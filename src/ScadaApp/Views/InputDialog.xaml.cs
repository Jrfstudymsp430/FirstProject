using System.Windows;
using System.Windows.Input;

namespace ScadaApp.Views;

public partial class InputDialog : Window
{
    public string Result { get; private set; } = string.Empty;

    public InputDialog(string title, string pointName, string dataType, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        PointNameText.Text = string.IsNullOrWhiteSpace(pointName) ? "—" : pointName.Trim();
        DataTypeText.Text = string.IsNullOrWhiteSpace(dataType) ? "—" : dataType.Trim();
        HintText.Text = dataType.Contains("Double", StringComparison.OrdinalIgnoreCase)
            ? "按功能码 16 写入四个寄存器，字序 GHEF CDAB"
            : dataType.Contains("Float", StringComparison.OrdinalIgnoreCase)
                ? "按功能码 16 写入两个寄存器，字序 CDAB"
                : "按功能码 06 写入单个保持寄存器";
        InputTextBox.Text = defaultValue;

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = InputTextBox.Text?.Trim() ?? string.Empty;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static string? Show(string title, string pointName, string dataType, string defaultValue = "")
    {
        var dialog = new InputDialog(title, pointName, dataType, defaultValue)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
