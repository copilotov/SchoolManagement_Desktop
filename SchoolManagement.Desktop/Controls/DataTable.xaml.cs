using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Localization;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - the shared list surface.
///
/// The table owns presentation state only (search text, column filters, sort,
/// page, density, selection). Rows come from the caller, so a screen keeps its
/// own query and use case; nothing here touches a repository.
///
/// Filtering and sorting run on a <see cref="ListCollectionView"/> and the page
/// is copied into a small <see cref="ObservableCollection{T}"/>, which keeps
/// the virtualizing <see cref="DataGrid"/> fast even for a long list. Column
/// definitions are <see cref="DataTableColumn"/> objects so a screen can bind a
/// header to <c>{loc:Tr ...}</c> and keep the language switch instant.
/// </summary>
public partial class DataTable : UserControl
{
    private static readonly FlowDirectionToAlignmentConverter AlignmentConverter = new();

    private readonly ObservableCollection<object> _pageItems = new();
    private readonly Dictionary<DataTableColumn, DataGridColumn> _columnMap = new();
    private readonly ContextMenu _rowMenu;
    private ListCollectionView? _view;
    private INotifyCollectionChanged? _observedSource;
    private string? _sortPath;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;
    private bool _isRebuilding;
    private bool _isSyncingSelection;

    private static readonly DependencyPropertyKey IsEmptyPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsEmpty), typeof(bool), typeof(DataTable), new PropertyMetadata(true));

    private static readonly DependencyPropertyKey IsDetailOpenPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsDetailOpen), typeof(bool), typeof(DataTable), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey ShowPagerPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ShowPager), typeof(bool), typeof(DataTable), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey SelectionCountPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SelectionCount), typeof(int), typeof(DataTable), new PropertyMetadata(0));

    private static readonly DependencyPropertyKey HasSelectionPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasSelection), typeof(bool), typeof(DataTable), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey CurrentPagePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CurrentPage), typeof(int), typeof(DataTable), new PropertyMetadata(1));

    private static readonly DependencyPropertyKey PageCountPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PageCount), typeof(int), typeof(DataTable), new PropertyMetadata(1));

    public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;
    public static readonly DependencyProperty IsDetailOpenProperty = IsDetailOpenPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ShowPagerProperty = ShowPagerPropertyKey.DependencyProperty;
    public static readonly DependencyProperty SelectionCountProperty = SelectionCountPropertyKey.DependencyProperty;
    public static readonly DependencyProperty HasSelectionProperty = HasSelectionPropertyKey.DependencyProperty;
    public static readonly DependencyProperty CurrentPageProperty = CurrentPagePropertyKey.DependencyProperty;
    public static readonly DependencyProperty PageCountProperty = PageCountPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(DataTable),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText), typeof(string), typeof(DataTable),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnFilterChanged));

    public static readonly DependencyProperty DensityProperty =
        DependencyProperty.Register(
            nameof(Density), typeof(DataTableDensity), typeof(DataTable),
            new PropertyMetadata(DataTableDensity.Comfortable, OnDensityChanged));

    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(
            nameof(PageSize), typeof(int), typeof(DataTable),
            new PropertyMetadata(25, OnPageSizeChanged));

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(
            nameof(IsBusy), typeof(bool), typeof(DataTable), new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty ShowSearchProperty =
        DependencyProperty.Register(
            nameof(ShowSearch), typeof(bool), typeof(DataTable), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowColumnChooserProperty =
        DependencyProperty.Register(
            nameof(ShowColumnChooser), typeof(bool), typeof(DataTable), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowDetailPanelProperty =
        DependencyProperty.Register(
            nameof(ShowDetailPanel), typeof(bool), typeof(DataTable), new PropertyMetadata(true, OnStateChanged));

    public static readonly DependencyProperty DetailTemplateProperty =
        DependencyProperty.Register(
            nameof(DetailTemplate), typeof(DataTemplate), typeof(DataTable), new PropertyMetadata(null, OnStateChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem), typeof(object), typeof(DataTable),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedItemChanged));

    public static readonly DependencyProperty ExportCommandProperty =
        DependencyProperty.Register(
            nameof(ExportCommand), typeof(ICommand), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty AddCommandProperty =
        DependencyProperty.Register(
            nameof(AddCommand), typeof(ICommand), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty RefreshCommandProperty =
        DependencyProperty.Register(
            nameof(RefreshCommand), typeof(ICommand), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(
            nameof(EditCommand), typeof(ICommand), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(
            nameof(DeleteCommand), typeof(ICommand), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty ToolbarContentProperty =
        DependencyProperty.Register(
            nameof(ToolbarContent), typeof(object), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty BulkActionsContentProperty =
        DependencyProperty.Register(
            nameof(BulkActionsContent), typeof(object), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty EmptyTitleProperty =
        DependencyProperty.Register(
            nameof(EmptyTitle), typeof(string), typeof(DataTable), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyDescriptionProperty =
        DependencyProperty.Register(
            nameof(EmptyDescription), typeof(string), typeof(DataTable), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyActionTextProperty =
        DependencyProperty.Register(
            nameof(EmptyActionText), typeof(string), typeof(DataTable), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyActionCommandProperty =
        DependencyProperty.Register(
            nameof(EmptyActionCommand), typeof(ICommand), typeof(DataTable), new PropertyMetadata(null));

    public static readonly DependencyProperty EmptyGlyphProperty =
        DependencyProperty.Register(
            nameof(EmptyGlyph), typeof(string), typeof(DataTable), new PropertyMetadata(string.Empty));

    public DataTable()
    {
        InitializeComponent();

        Columns.CollectionChanged += OnColumnsCollectionChanged;
        Table.ItemsSource = _pageItems;

        _rowMenu = BuildContextMenu();
        Table.ContextMenu = _rowMenu;

        ApplyDensity();
        UpdateState();
    }

    /// <summary>
    /// The column definitions. Declared in XAML as
    /// <c>&lt;controls:DataTable.Columns&gt;</c> so a header can bind a
    /// translation key.
    /// </summary>
    public ObservableCollection<DataTableColumn> Columns { get; } = new();

    public bool IsEmpty => (bool)GetValue(IsEmptyProperty);

    public bool IsDetailOpen => (bool)GetValue(IsDetailOpenProperty);

    public bool ShowPager => (bool)GetValue(ShowPagerProperty);

    public int SelectionCount => (int)GetValue(SelectionCountProperty);

    public bool HasSelection => (bool)GetValue(HasSelectionProperty);

    public int CurrentPage => (int)GetValue(CurrentPageProperty);

    public int PageCount => (int)GetValue(PageCountProperty);

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public DataTableDensity Density
    {
        get => (DataTableDensity)GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public bool ShowSearch
    {
        get => (bool)GetValue(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
    }

    public bool ShowColumnChooser
    {
        get => (bool)GetValue(ShowColumnChooserProperty);
        set => SetValue(ShowColumnChooserProperty, value);
    }

    public bool ShowDetailPanel
    {
        get => (bool)GetValue(ShowDetailPanelProperty);
        set => SetValue(ShowDetailPanelProperty, value);
    }

    public DataTemplate? DetailTemplate
    {
        get => (DataTemplate?)GetValue(DetailTemplateProperty);
        set => SetValue(DetailTemplateProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ICommand? ExportCommand
    {
        get => (ICommand?)GetValue(ExportCommandProperty);
        set => SetValue(ExportCommandProperty, value);
    }

    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public ICommand? RefreshCommand
    {
        get => (ICommand?)GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public object? ToolbarContent
    {
        get => GetValue(ToolbarContentProperty);
        set => SetValue(ToolbarContentProperty, value);
    }

    public object? BulkActionsContent
    {
        get => GetValue(BulkActionsContentProperty);
        set => SetValue(BulkActionsContentProperty, value);
    }

    public string EmptyTitle
    {
        get => (string)GetValue(EmptyTitleProperty);
        set => SetValue(EmptyTitleProperty, value);
    }

    public string EmptyDescription
    {
        get => (string)GetValue(EmptyDescriptionProperty);
        set => SetValue(EmptyDescriptionProperty, value);
    }

    public string EmptyActionText
    {
        get => (string)GetValue(EmptyActionTextProperty);
        set => SetValue(EmptyActionTextProperty, value);
    }

    public ICommand? EmptyActionCommand
    {
        get => (ICommand?)GetValue(EmptyActionCommandProperty);
        set => SetValue(EmptyActionCommandProperty, value);
    }

    public string EmptyGlyph
    {
        get => (string)GetValue(EmptyGlyphProperty);
        set => SetValue(EmptyGlyphProperty, value);
    }

    /// <summary>Rebuilds the filtered view from <see cref="ItemsSource"/>.</summary>
    public void Refresh()
    {
        if (Table is null)
        {
            return;
        }

        var source = ItemsSource;
        if (source is null)
        {
            _view = null;
            _pageItems.Clear();
            PageTo(1);
            return;
        }

        var list = source as IList ?? source.Cast<object>().ToList();
        _view = new ListCollectionView(list)
        {
            Filter = MatchesFilter,
        };

        ApplySortDescription();
        PageTo(1);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var table = (DataTable)d;

        if (table._observedSource is not null)
        {
            table._observedSource.CollectionChanged -= table.OnSourceCollectionChanged;
        }

        table._observedSource = e.NewValue as INotifyCollectionChanged;
        if (table._observedSource is not null)
        {
            table._observedSource.CollectionChanged += table.OnSourceCollectionChanged;
        }

        table.Refresh();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataTable)d).ApplyFilter();

    private static void OnDensityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataTable)d).ApplyDensity();

    private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataTable)d).PageTo(1);

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataTable)d).UpdateState();

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataTable)d).SyncSelectionFromProperty();

    private void ApplyFilter()
    {
        if (_view is null)
        {
            PageTo(1);
            return;
        }

        _view.Refresh();
        PageTo(1);
    }

    private bool MatchesFilter(object item)
        => DataTableFilter.Matches(item, Columns.ToList(), SearchText);

    private void ApplySortDescription()
    {
        if (_view is null || string.IsNullOrWhiteSpace(_sortPath))
        {
            return;
        }

        using (_view.DeferRefresh())
        {
            _view.SortDescriptions.Clear();
            _view.SortDescriptions.Add(new SortDescription(_sortPath, _sortDirection));
        }
    }

    private void PageTo(int page)
    {
        if (Table is null)
        {
            return;
        }

        var count = _view?.Count ?? 0;
        var pageSize = PageSize <= 0 ? Math.Max(1, count) : PageSize;
        var pageCount = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
        var target = Math.Clamp(page, 1, pageCount);

        _isSyncingSelection = true;
        try
        {
            _pageItems.Clear();
            if (_view is not null)
            {
                foreach (var item in _view.Cast<object>().Skip((target - 1) * pageSize).Take(pageSize))
                {
                    _pageItems.Add(item);
                }
            }

            Table.SelectedItems.Clear();
        }
        finally
        {
            _isSyncingSelection = false;
        }

        SetValue(CurrentPagePropertyKey, target);
        SetValue(PageCountPropertyKey, pageCount);
        SetValue(SelectionCountPropertyKey, 0);
        SetValue(HasSelectionPropertyKey, false);
        SetCurrentValue(SelectedItemProperty, null);
        UpdateState();
    }

    private void UpdateState()
    {
        if (Table is null)
        {
            return;
        }

        var isEmpty = !IsBusy && (_view is null || _view.Count == 0);
        SetValue(IsEmptyPropertyKey, isEmpty);
        SetValue(IsDetailOpenPropertyKey, ShowDetailPanel && SelectedItem is not null && DetailTemplate is not null);
        SetValue(ShowPagerPropertyKey, PageSize > 0 && PageCount > 1);

        Table.Visibility = isEmpty || IsBusy ? Visibility.Collapsed : Visibility.Visible;

        if (PreviousButton is not null)
        {
            PreviousButton.IsEnabled = CurrentPage > 1;
        }

        if (NextButton is not null)
        {
            NextButton.IsEnabled = CurrentPage < PageCount;
        }
    }

    private void ApplyDensity()
    {
        if (Table is null)
        {
            return;
        }

        if (Density == DataTableDensity.Compact)
        {
            Table.SetResourceReference(FrameworkElement.StyleProperty, "DataGrid.Compact");
        }
        else
        {
            Table.ClearValue(FrameworkElement.StyleProperty);
        }
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DataTableColumn column in e.OldItems)
            {
                column.Changed -= OnColumnChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (DataTableColumn column in e.NewItems)
            {
                column.Changed += OnColumnChanged;
            }
        }

        RebuildColumns();
        ApplyFilter();
    }

    private void OnColumnChanged(object? sender, DataTableColumnChangedEventArgs e)
    {
        if (sender is not DataTableColumn column)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(DataTableColumn.IsVisible):
                ApplyColumnVisibility();
                break;

            case nameof(DataTableColumn.Width):
                if (_columnMap.TryGetValue(column, out var mapped))
                {
                    mapped.Width = column.Width;
                }

                break;

            case nameof(DataTableColumn.FilterText):
                ApplyFilter();
                break;

            case nameof(DataTableColumn.Header):
                // The header TextBlock binds to the column, so it updates itself.
                break;

            default:
                RebuildColumns();
                break;
        }
    }

    private void RebuildColumns()
    {
        if (Table is null || _isRebuilding)
        {
            return;
        }

        _isRebuilding = true;
        try
        {
            Table.Columns.Clear();
            _columnMap.Clear();

            foreach (var column in Columns)
            {
                var gridColumn = CreateColumn(column);
                _columnMap[column] = gridColumn;
                Table.Columns.Add(gridColumn);

                if (!string.IsNullOrWhiteSpace(_sortPath)
                    && string.Equals(gridColumn.SortMemberPath, _sortPath, StringComparison.Ordinal))
                {
                    gridColumn.SortDirection = _sortDirection;
                }
            }
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private DataGridColumn CreateColumn(DataTableColumn column)
    {
        DataGridColumn gridColumn;

        if (column.CellTemplate is { } template)
        {
            gridColumn = new DataGridTemplateColumn { CellTemplate = template };
        }
        else
        {
            var binding = new Binding(column.BindingPath)
            {
                ConverterCulture = CultureInfo.CurrentCulture,
            };

            if (!string.IsNullOrWhiteSpace(column.StringFormat))
            {
                binding.StringFormat = column.StringFormat;
            }

            gridColumn = new DataGridTextColumn
            {
                Binding = binding,
                ElementStyle = BuildCellStyle(column.IsNumeric),
            };
        }

        gridColumn.Header = BuildHeader(column);
        gridColumn.Width = column.Width;
        gridColumn.SortMemberPath = string.IsNullOrWhiteSpace(column.SortMemberPath)
            ? column.BindingPath
            : column.SortMemberPath;
        gridColumn.CanUserSort = !string.IsNullOrWhiteSpace(gridColumn.SortMemberPath);
        gridColumn.Visibility = column.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        return gridColumn;
    }

    private static object BuildHeader(DataTableColumn column)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 2, 0, 2),
        };

        var title = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(DataTableColumn.Header)) { Source = column });
        title.SetResourceReference(TextBlock.ForegroundProperty, "Text.Secondary");
        panel.Children.Add(title);

        var filter = new TextBox
        {
            Height = 24,
            MinHeight = 24,
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(6, 0, 6, 0),
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        filter.SetBinding(TextBox.TextProperty, new Binding(nameof(DataTableColumn.FilterText))
        {
            Source = column,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        filter.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(DataTableColumn.Header)) { Source = column });
        panel.Children.Add(filter);

        return panel;
    }

    private static Style BuildCellStyle(bool numeric)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(
            TextBlock.TextAlignmentProperty,
            new Binding(nameof(FlowDirection))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1),
                Converter = AlignmentConverter,
                ConverterParameter = numeric ? "Trailing" : "Leading",
            }));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        return style;
    }

    private void ApplyColumnVisibility()
    {
        foreach (var (column, gridColumn) in _columnMap)
        {
            gridColumn.Visibility = column.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var details = new MenuItem();
        BindLocalized(details, TranslationKeys.Common.Details);
        details.SetBinding(MenuItem.CommandProperty, new Binding(nameof(EditCommand)) { Source = this });

        var edit = new MenuItem();
        BindLocalized(edit, TranslationKeys.Common.Edit);
        edit.SetBinding(MenuItem.CommandProperty, new Binding(nameof(EditCommand)) { Source = this });
        edit.SetBinding(
            UIElement.VisibilityProperty,
            new Binding(nameof(EditCommand))
            {
                Source = this,
                Converter = new ObjectToVisibilityConverter(),
            });

        var delete = new MenuItem();
        BindLocalized(delete, TranslationKeys.Common.Delete);
        delete.SetBinding(MenuItem.CommandProperty, new Binding(nameof(DeleteCommand)) { Source = this });
        delete.SetBinding(
            UIElement.VisibilityProperty,
            new Binding(nameof(DeleteCommand))
            {
                Source = this,
                Converter = new ObjectToVisibilityConverter(),
            });

        menu.Items.Add(details);
        menu.Items.Add(new Separator());
        menu.Items.Add(edit);
        menu.Items.Add(delete);
        return menu;
    }

    private static void BindLocalized(HeaderedItemsControl target, string key)
    {
        var localization = LocalizationProvider.Current;
        if (localization is null)
        {
            target.Header = key;
            return;
        }

        target.SetBinding(
            HeaderedItemsControl.HeaderProperty,
            new Binding($"[{key}]") { Source = localization, Mode = BindingMode.OneWay });
    }

    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        var path = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (string.Equals(_sortPath, path, StringComparison.Ordinal))
        {
            _sortDirection = _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            _sortPath = path;
            _sortDirection = ListSortDirection.Ascending;
        }

        foreach (var column in Table.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = _sortDirection;
        ApplySortDescription();
        PageTo(1);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || Table is null)
        {
            return;
        }

        var count = Table.SelectedItems.Count;
        SetValue(SelectionCountPropertyKey, count);
        SetValue(HasSelectionPropertyKey, count > 0);

        _isSyncingSelection = true;
        try
        {
            SetCurrentValue(SelectedItemProperty, Table.SelectedItem);
        }
        finally
        {
            _isSyncingSelection = false;
        }

        UpdateState();
    }

    private void SyncSelectionFromProperty()
    {
        if (Table is null)
        {
            return;
        }

        if (!ReferenceEquals(Table.SelectedItem, SelectedItem))
        {
            _isSyncingSelection = true;
            try
            {
                Table.SelectedItem = SelectedItem;
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }

        UpdateState();
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var row = ItemsControl.ContainerFromElement(Table, source) as DataGridRow;
        if (row is null)
        {
            return;
        }

        row.IsSelected = true;
        Table.SelectedItem = row.Item;
    }

    private void OnColumnsClick(object sender, RoutedEventArgs e)
        => ColumnsPopup.IsOpen = !ColumnsPopup.IsOpen;

    private void OnDensityClick(object sender, RoutedEventArgs e)
        => Density = Density == DataTableDensity.Compact
            ? DataTableDensity.Comfortable
            : DataTableDensity.Compact;

    private void OnClearSelectionClick(object sender, RoutedEventArgs e)
        => Table.UnselectAll();

    private void OnPreviousPageClick(object sender, RoutedEventArgs e) => PageTo(CurrentPage - 1);

    private void OnNextPageClick(object sender, RoutedEventArgs e) => PageTo(CurrentPage + 1);
}
