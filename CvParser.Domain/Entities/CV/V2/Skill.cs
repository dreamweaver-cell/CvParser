namespace CvParser.Domain.Entities.CV.V2;

public class Skill
{
    public string? Name { get; set; }
    public string? Level { get; set; }
    public List<SkillKeyword>? Keywords { get; set; }
}