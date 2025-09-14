using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class Employer
{
    [JsonPropertyName("company")]
    public string? Company { get; set; }
    [JsonPropertyName("position")]
    public string? Position { get; set; }
    [JsonPropertyName("period")]
    public string? Period { get; set; }
}
