using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class PersonalInformation
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("imageBase64")]
    public string? ImageBase64 { get; set; }
    [JsonPropertyName("picture")]
    public byte[]? Picture { get; set; }
}
