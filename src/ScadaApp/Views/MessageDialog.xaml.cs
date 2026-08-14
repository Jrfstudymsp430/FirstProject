using System.Windows;
using System.Windows.Input;

namespace ScadaApp.Views;

public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message, bool showCancel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static void Alert(string title, string message)
    {
        var dialog = new MessageDialog(title, message, showCancel: false)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    public static bool Confirm(string title, string message)
    {
        var dialog = new MessageDialog(title, message, showCancel: true)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true;
    }
}
