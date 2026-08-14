using System.Windows;
using System.Windows.Controls;
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
            ModbusFunctionCode.ReadHoldingRegisters,
            ModbusFunctionCode.WriteSingleRegister,
            ModbusFunctionCode.WriteMultipleRegisters
        };

        DataTypeCombo.ItemsSource = new[]
        {
            TagDataType.Float32,
            TagDataType.UInt16
        };

        DataContext = Point;
        FunctionCodeCombo.SelectionChanged += FunctionCodeCombo_SelectionChanged;
    }

    private void FunctionCodeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FunctionCodeCombo.SelectedItem is ModbusFunctionCode.WriteMultipleRegisters)
            Point.DataType = TagDataType.Float32;
        else if (FunctionCodeCombo.SelectedItem is ModbusFunctionCode.WriteSingleRegister)
            Point.DataType = TagDataType.UInt16;
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
