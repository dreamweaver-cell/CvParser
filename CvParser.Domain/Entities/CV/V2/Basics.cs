using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V2;

public class Basics
{
    public string? Name { get; set; }
    public string? Label { get; set; }
    [JsonPropertyName("image")]
    public string? ImageBase64 { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Url { get; set; }
    public string? Summary { get; set; }
    public Location Location { get; set; }
    public List<Profile> Profiles { get; set; }    
}
