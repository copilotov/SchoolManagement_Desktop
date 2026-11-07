using System.Windows.Controls;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Shared add/edit student form. The same instance is used for both modes, so
/// the two flows can never drift apart.
/// </summary>
public partial class StudentFormView : UserControl
{
    public StudentFormView()
    {
        InitializeComponent();
    }
}
