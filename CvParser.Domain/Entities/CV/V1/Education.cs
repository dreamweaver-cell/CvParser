using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class Education
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("degree")]
    public string? Degree { get; set; }
    [JsonPropertyName("institution")]
    public string? Institution { get; set; }
    [JsonPropertyName("period")]
    public string? Period { get; set; }
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }
    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }
    [JsonPropertyName("studyType")]
    public string? StudyType { get; set; }
}
