namespace ScadaApp.Models;

public class CalibrationPoint
{
    public double Measured { get; set; }
    public double Standard { get; set; }
}

public enum CalibrationWriteMode
{
    Table = 0,
    SlopeIntercept = 1
}
