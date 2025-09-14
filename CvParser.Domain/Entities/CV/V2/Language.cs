using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V2;

public class LanguageV2
{
    [JsonPropertyName("language")]
    public string? LanguageName { get; set; }
    public string? Fluency { get; set; }
}
