using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
#if DEBUG
using Microsoft.Extensions.DependencyInjection;
#endif

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Phase D2 - the shell window.
///
/// Responsibilities that stay in the view:
///  * one cached view per ViewModel, so scroll position and inline editor state
///    survive a round trip to another destination;
///  * the light fade + slide transition between pages;
///  * the global keyboard map (the ViewModel owns the behaviour, the window only
///    translates a gesture into a <see cref="ShortcutAction"/>);
///  * restoring and persisting the window geometry;
///  * creating the two owned windows (shortcuts, debug gallery).
/// </summary>
public partial class ShellWindow : FluentWindow
{
    private readonly ShellViewModel _viewModel;
    private readonly IWindowStateStore _windowState;
    private readonly Dictionary<object, FrameworkElement> _pages = new();

    private ShortcutsWindow? _shortcuts;

#if DEBUG
    private readonly IServiceProvider _services;
    private DesignGalleryWindow? _gallery;
#endif

    public ShellWindow(
        ShellViewModel viewModel,
        IWindowStateStore windowState
#if DEBUG
        , IServiceProvider services
#endif
        )
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowState = windowState;
        DataContext = viewModel;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.ShortcutsRequested += OnShortcutsRequested;

#if DEBUG
        _services = services;
#endif

        // The license is re-evaluated every time the window regains focus, so a
        // license that expires while the app is open becomes read-only without
        // a restart. A failure here must never take the shell down.
        Activated += OnActivated;
        SizeChanged += OnSizeChanged;

        ShowCurrentPage();
    }

    /// <summary>
    /// Restores the saved geometry before the window is shown, so the shell never
    /// appears at the default size and then jumps. The placement is clamped to the
    /// current virtual desktop, which keeps a window reachable after a monitor is
    /// unplugged or a resolution changes.
    /// </summary>
    public async Task InitializePlacementAsync(CancellationToken cancellationToken = default)
    {
        var saved = await _windowState.LoadAsync(cancellationToken).ConfigureAwait(true);

        var placement = saved.Clamp(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight,
            MinWidth,
            MinHeight);

        Width = placement.Width;
        Height = placement.Height;

        if (!double.IsNaN(saved.Left) && !double.IsNaN(saved.Top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = placement.Left;
            Top = placement.Top;
        }

        if (saved.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private async void OnActivated(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.RefreshLicenseAsync();
            await _viewModel.RefreshUnreadAsync();
        }
        catch (OperationCanceledException)
        {
            // Closing the window cancels the refresh.
        }
        catch (Exception)
        {
            // The repository already converts expected failures to data; this
            // is the last-resort guard for the UI thread.
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        _viewModel.UpdateWindowWidth(e.NewSize.Width);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentViewModel))
        {
            ShowCurrentPage();
        }
    }

    /// <summary>
    /// Materializes the current page once and keeps it. Reusing the same visual
    /// is what preserves a table's scroll offset and an open inline editor when
    /// the user leaves a destination and comes back.
    /// </summary>
    private void ShowCurrentPage()
    {
        if (_viewModel.CurrentViewModel is not { } viewModel)
        {
            return;
        }

        if (!_pages.TryGetValue(viewModel, out var page))
        {
            page = CreatePage(viewModel);
            _pages[viewModel] = page;
            PageHost.Children.Add(page);
        }

        foreach (UIElement child in PageHost.Children)
        {
            child.Visibility = ReferenceEquals(child, page)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        PlayPageTransition(page);
    }

    private FrameworkElement CreatePage(object viewModel)
    {
        var template = TryFindResource(new DataTemplateKey(viewModel.GetType())) as DataTemplate
            ?? throw new InvalidOperationException(
                $"No DataTemplate is registered for '{viewModel.GetType().FullName}'.");

        var element = (FrameworkElement)template.LoadContent();
        element.DataContext = viewModel;
        return element;
    }

    /// <summary>
    /// Fade + 8px slide, 180ms, opacity and render transform only. It returns
    /// immediately when Windows "Animation effects" is off, and it never animates
    /// layout, so a transition cannot trigger a re-measure of the page.
    /// </summary>
    private static void PlayPageTransition(UIElement page)
    {
        if (!MotionSettings.IsMotionEnabled)
        {
            page.Opacity = 1;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(180);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        page.Opacity = 0;
        page.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = easing });

        var canTranslate = page.RenderTransform is null
            || ReferenceEquals(page.RenderTransform, Transform.Identity);

        if (!canTranslate)
        {
            return;
        }

        var translate = new TranslateTransform(0, 8);
        page.RenderTransform = translate;
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, duration) { EasingFunction = easing });
    }

    private void OnShortcutsRequested(object? sender, EventArgs e) => ShowShortcutsWindow();

    private void ShowShortcutsWindow()
    {
        if (_shortcuts is { IsVisible: true })
        {
            _shortcuts.Activate();
            return;
        }

        _shortcuts = new ShortcutsWindow { Owner = this };
        _shortcuts.Closed += (_, _) => _shortcuts = null;
        _shortcuts.Show();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        // Global shortcuts. Ctrl+S and Ctrl+P stay page-owned: a page knows what
        // saving or printing means in its own context, the shell does not.
        if (key == Key.F1 && modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            _viewModel.ExecuteShortcut(ShortcutAction.Help);
            return;
        }

        if (modifiers == ModifierKeys.Control)
        {
            switch (key)
            {
                case Key.K:
                    e.Handled = true;
                    _viewModel.ExecuteShortcut(ShortcutAction.CommandPalette);
                    return;

                // Ctrl+F is the documented global search alias; it opens the same
                // palette so a user coming from a browser or an editor finds it.
                case Key.F:
                    e.Handled = true;
                    _viewModel.ExecuteShortcut(ShortcutAction.Search);
                    return;

                case Key.N:
                    e.Handled = true;
                    _viewModel.ExecuteShortcut(ShortcutAction.New);
                    return;

                case Key.B:
                    e.Handled = true;
                    _viewModel.ExecuteShortcut(ShortcutAction.ToggleNav);
                    return;
            }
        }

        if (modifiers == ModifierKeys.Alt)
        {
            if (key == Key.Left)
            {
                e.Handled = true;
                _viewModel.ExecuteShortcut(ShortcutAction.Back);
                return;
            }

            // Alt+1..5 jump to the first destination of each navigation group.
            var destination = key switch
            {
                Key.D1 or Key.NumPad1 => ShellTab.Dashboard,
                Key.D2 or Key.NumPad2 => ShellTab.Students,
                Key.D3 or Key.NumPad3 => ShellTab.Finance,
                Key.D4 or Key.NumPad4 => ShellTab.Notifications,
                Key.D5 or Key.NumPad5 => ShellTab.Settings,
                _ => (ShellTab?)null,
            };

            if (destination is { } tab)
            {
                e.Handled = true;
                _viewModel.ExecuteShortcut(ShortcutAction.Navigate, tab);
                return;
            }
        }

        if (key == Key.Escape && _viewModel.CommandPalette.IsOpen)
        {
            e.Handled = true;
            _viewModel.ExecuteShortcut(ShortcutAction.Close);
            return;
        }

#if DEBUG
        if (key == Key.F12)
        {
            e.Handled = true;
            ShowDesignGallery();
        }
#endif
    }

#if DEBUG
    /// <summary>
    /// Phase D1 - F12 opens the design gallery in a debug build. The whole
    /// handler is compiled out of release, so the shipped app has no hidden
    /// developer surface. One instance is reused so a second F12 focuses the
    /// window that is already open instead of stacking copies.
    /// </summary>
    private void ShowDesignGallery()
    {
        if (_gallery is { IsVisible: true })
        {
            _gallery.Activate();
            return;
        }

        _gallery = _services.GetRequiredService<DesignGalleryWindow>();
        _gallery.Owner = this;
        _gallery.Show();
    }
#endif

    protected override void OnClosed(EventArgs e)
    {
        Activated -= OnActivated;
        SizeChanged -= OnSizeChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ShortcutsRequested -= OnShortcutsRequested;

        _shortcuts?.Close();
        _shortcuts = null;

        PersistPlacement();

#if DEBUG
        _gallery?.Close();
        _gallery = null;
#endif

        base.OnClosed(e);
    }

    /// <summary>
    /// Saves the normal-state bounds, not the maximized bounds, so restoring a
    /// maximized window still remembers the size it was before the user
    /// maximized it.
    /// </summary>
    private void PersistPlacement()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var placement = new WindowPlacement(
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            WindowState == WindowState.Maximized);

        // Closing must persist before the process exits, so this one write is
        // synchronous. It is a few hundred bytes and cannot deadlock: the store
        // resumes on the thread pool.
        _windowState.SaveAsync(placement).GetAwaiter().GetResult();
    }
}
