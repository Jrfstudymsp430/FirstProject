using System.Windows;
using System.Windows.Input;
using ScadaApp.Services;
using ScadaApp.ViewModels;

namespace ScadaApp.Views;

public sealed class BatchWriteRow
{
    public required string TagId { get; init; }
    public required string Name { get; init; }
    public ushort Address { get; init; }
    public required string DataType { get; init; }
    public string Input { get; set; } = string.Empty;
}

public partial class BatchWriteDialog : Window
{
    private readonly List<BatchWriteRow> _rows;

    public IReadOnlyList<BatchWriteRow>? Result { get; private set; }

    public BatchWriteDialog(IEnumerable<TagItemViewModel> tags)
    {
        InitializeComponent();
        _rows = tags
            .OrderBy(t => t.Address)
            .Select(t => new BatchWriteRow
            {
                TagId = t.TagId,
                Name = t.Name,
                Address = t.Address,
                DataType = t.DataType,
                Input = DefaultInput(t.DisplayValue)
            })
            .ToList();

        RowList.ItemsSource = _rows;
        if (_rows.Count > 0)
        {
            var start = _rows[0].Address;
            var last = tags.OrderBy(t => t.Address).Last();
            var end = last.Address + last.Point.RegisterCount - 1;
            SummaryText.Text = $"将用功能码 16 一次写入 {_rows.Count} 个连续点（地址 {start}–{end}）";
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Any(r => string.IsNullOrWhiteSpace(r.Input)))
        {
            MessageDialog.Alert("批量写入", "请为每个点填写写入值。");
            return;
        }

        Result = _rows;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static IReadOnlyList<BatchWriteRow>? Show(IEnumerable<TagItemViewModel> tags)
    {
        var dialog = new BatchWriteDialog(tags)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private static string DefaultInput(string display)
    {
        if (display is "ERR" or "--")
            return "0";

        return TagBlockWriter.StripUnit(display);
    }
}
