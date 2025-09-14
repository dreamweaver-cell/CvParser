namespace CvParser.Domain.Entities.CV.V2;

public class Project
{
    public string? Name { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Description { get; set; }
    public List<string>?Highlights { get; set; }
    public string? Url { get; set; }
}