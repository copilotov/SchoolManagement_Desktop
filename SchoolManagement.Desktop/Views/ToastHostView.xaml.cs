using System.Windows.Controls;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Phase D2 - the toast host. It is a pure projection of
/// <c>IToastService.Items</c>; the service owns the lifetime and the cap.
/// </summary>
public partial class ToastHostView : UserControl
{
    public ToastHostView()
    {
        InitializeComponent();
    }
}
