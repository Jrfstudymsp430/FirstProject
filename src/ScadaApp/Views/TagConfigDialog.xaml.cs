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
            TagDataType.Double64,
            TagDataType.UInt16
        };

        DecimalPlacesCombo.ItemsSource = Enumerable.Range(0, 13).ToArray();

        DataContext = Point;
        FunctionCodeCombo.SelectionChanged += FunctionCodeCombo_SelectionChanged;
        DataTypeCombo.SelectionChanged += DataTypeCombo_SelectionChanged;
    }

    private void FunctionCodeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FunctionCodeCombo.SelectedItem is ModbusFunctionCode.WriteSingleRegister)
            Point.DataType = TagDataType.UInt16;
        else if (FunctionCodeCombo.SelectedItem is ModbusFunctionCode.WriteMultipleRegisters
                 && Point.DataType == TagDataType.UInt16)
            Point.DataType = TagDataType.Float32;
    }

    private void DataTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Point.DataType is TagDataType.Float32 or TagDataType.Double64
            && Point.FunctionCode == ModbusFunctionCode.WriteSingleRegister)
            Point.FunctionCode = ModbusFunctionCode.WriteMultipleRegisters;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Point.Name = Point.Name?.Trim() ?? string.Empty;
        Point.Unit = Point.Unit?.Trim() ?? string.Empty;
        Point.DecimalPlaces = Math.Clamp(Point.DecimalPlaces, 0, 12);
        if (Point.DataType is TagDataType.Float32 or TagDataType.Double64
            && Point.FunctionCode == ModbusFunctionCode.WriteSingleRegister)
            Point.FunctionCode = ModbusFunctionCode.WriteMultipleRegisters;
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
