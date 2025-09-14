using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class WorkExperience
{
    [JsonPropertyName("workExperience")]
    public string? Experience { get; set; }
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }
    [JsonPropertyName("Position")]
    public string? Position { get; set; }
    [JsonPropertyName("startdate")]
    public string? StartDate { get; set; }
    [JsonPropertyName("enddate")]
    public string? EndDate { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("technologies")]
    public string? Technologies { get; set; }
}
