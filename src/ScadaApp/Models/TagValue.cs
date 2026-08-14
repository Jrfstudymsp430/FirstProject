namespace ScadaApp.Models;

public class TagValue
{
    public string TagId { get; set; } = string.Empty;
    public object? RawValue { get; set; }
    public string DisplayValue { get; set; } = "--";
    public string Quality { get; set; } = "Bad";
    public DateTime Timestamp { get; set; } = DateTime.MinValue;
    public string? ErrorMessage { get; set; }
}
