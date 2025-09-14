using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class Language
{
    [JsonPropertyName("level")]
    public string? Level { get; set; }
    [JsonPropertyName("language")]
    public string? LanguageName { get; set; }
    [JsonPropertyName("fluency")]
    public string? Fluency { get; set; }
}
