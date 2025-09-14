using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class Certificate
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("Institution")]
    public string? Institution { get; set; }
}
