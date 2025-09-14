namespace CvParser.Infrastructure.Config
{
    public class OpenAiSettings
    {
        public string BaseUrl { get; set; } = "https://api.openai.com/";
        public string ApiKey { get; set; } = string.Empty;
    }
}
