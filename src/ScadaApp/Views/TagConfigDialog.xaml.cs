using System.Windows;
using System.Windows.Input;
using ScadaApp.Models;

namespace ScadaApp.Views;

public partial class TagConfigDialog : Window
{
    public TagPoint Point { get; }

    public TagConfigDialog(TagPoint tag)
    {
        Point = tag;
        InitializeComponent();

        FunctionCodeCombo.ItemsSource = new[]
        {
            ModbusFunctionCode.ReadCoils,
            ModbusFunctionCode.ReadDiscreteInputs,
            ModbusFunctionCode.ReadHoldingRegisters,
            ModbusFunctionCode.ReadInputRegisters
        };

        DataTypeCombo.ItemsSource = new[]
        {
            TagDataType.Bool,
            TagDataType.Int16,
            TagDataType.UInt16,
            TagDataType.Int32,
            TagDataType.UInt32,
            TagDataType.Float32
        };

        DataContext = Point;
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

    public static bool Edit(TagPoint tag)
    {
        var dialog = new TagConfigDialog(tag)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true;
    }
}
