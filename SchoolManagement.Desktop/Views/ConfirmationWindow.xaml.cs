using System.Windows;
using Wpf.Ui.Controls;
using SchoolManagement.Desktop.Localization;

namespace SchoolManagement.Desktop.Views;

public partial class ConfirmationWindow : FluentWindow
{
    public ConfirmationWindow()
    {
        InitializeComponent();
        FlowDirection = LocalizationProvider.Current?.IsRightToLeft == true
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    /// <summary>
    /// Shows the dialog and returns the user's answer. A null cancel text turns
    /// the dialog into an information box with a single OK button.
    /// </summary>
    public bool? Configure(string title, string message, string confirmText, string? cancelText)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (string.IsNullOrWhiteSpace(cancelText))
        {
            CancelButton.Visibility = Visibility.Collapsed;
            IconText.Text = TryFindResource("Icon.Info") as string ?? IconText.Text;
        }
        else
        {
            CancelButton.Content = cancelText;
            IconText.Text = TryFindResource("Icon.Help") as string ?? IconText.Text;
        }

        return ShowDialog();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
