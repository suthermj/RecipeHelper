using System.ComponentModel.DataAnnotations;

public class Measurement
{
    public int Id { get; set; }
    public string Name { get; set; } // e.g., "teaspoon", "cup", "gram"
    public string MeasureType { get; set; } // e.g., "volume", "weight"
}