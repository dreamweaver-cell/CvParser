using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class Cv
{
    [JsonPropertyName("personalInformation")]
    public PersonalInformation? PersonalInfo { get; set; } = new();
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    [JsonPropertyName("educations")]
    public List<Education>? Educations { get; set; }
    [JsonPropertyName("employers")]
    public List<Employer>? Employers { get; set; }
    [JsonPropertyName("workExperiences")]
    public List<WorkExperience>? WorkExperiences { get; set; }
    [JsonPropertyName("competencies")]
    public List<Competency>? Competencies { get; set; }
    [JsonPropertyName("certificates")]
    public List<Certificate>? Certificates { get; set; }
    [JsonPropertyName("languages")]
    public List<Language>? Languages { get; set; }
    [JsonPropertyName("other")]
    public List<string?>? Other { get; set; }
    [JsonPropertyName("locale")]
    public string? Locale { get; set; }
}
