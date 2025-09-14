using System.Text.Json.Serialization;

namespace CvParser.Domain.Entities.CV.V1;

public class Competency
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("level")]
    public string? Level { get; set; }
    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }
    [JsonPropertyName("isMainSkill")]
    public bool IsMainSkill { get; set; }
    [JsonPropertyName("skillCategory")]
    public string? SkillCategory { get; set; }
}
