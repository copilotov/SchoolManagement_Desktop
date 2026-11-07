using System.Collections.ObjectModel;
using System.Windows.Threading;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// UI-thread toast queue. At most four toasts are visible at once; the oldest
/// is dropped first so a burst of failures cannot grow the visual tree without
/// bound. Each toast closes itself after a few seconds, and a click on the close
/// button removes it immediately.
/// </summary>
public sealed class ToastService : IToastService
{
    private const int MaxVisible = 4;
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(4);

    private readonly ObservableCollection<ToastItem> _items = new();

    public ToastService()
    {
        Items = new ReadOnlyObservableCollection<ToastItem>(_items);
    }

    public ReadOnlyObservableCollection<ToastItem> Items { get; }

    public void Show(string message, ToastSeverity severity = ToastSeverity.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var item = new ToastItem(message, severity, Dismiss);
        _items.Insert(0, item);

        while (_items.Count > MaxVisible)
        {
            _items.RemoveAt(_items.Count - 1);
        }

        StartLifetimeTimer(item);
    }

    public void ShowSuccess(string message) => Show(message, ToastSeverity.Success);

    public void ShowWarning(string message) => Show(message, ToastSeverity.Warning);

    public void ShowError(string message) => Show(message, ToastSeverity.Danger);

    public void Dismiss(ToastItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Remove(item);
    }

    private static void StartLifetimeTimer(ToastItem item)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = Lifetime,
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            item.RequestClose();
        };

        timer.Start();
    }
}
