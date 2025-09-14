using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V2;

public class PersonalInformation
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("birthplace")]
    public string? Birthplace { get; set; }
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }
    [JsonPropertyName("imageBase64")]
    public string? ImageBase64 { get; set; }
    [JsonPropertyName("picture")]
    public byte[]? Picture { get; set; }
}
