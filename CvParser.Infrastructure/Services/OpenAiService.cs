using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;


namespace CvParser.Infrastructure.Services
{
    public class OpenAiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiService> _logger;

        // Configuration settings
        private readonly string _aiModel;
        private readonly string _completionUrl;
        private readonly string _question;

        public OpenAiService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _aiModel = configuration["OpenAI:Model"]
                ?? throw new ArgumentException("OpenAI:Model must be set in configuration.");

            _completionUrl = configuration["OpenAI:CompletionUrl"]
                ?? throw new ArgumentException("OpenAI:CompletionUrl must be set in configuration.");

            _question = configuration["OpenAI:Question"] ?? "Please process the CV text.";
        }


        // Core method to send prompt to OpenAI and get response
        public async Task<string> GenerateContentAsync(string prompt)
        {
            try
            {
                var schema = await ReadEmbeddedResourceAsync("CvSchema.json");

                var requestBody = new
                {
                    model = _aiModel,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a CV parser. Always return JSON that matches the provided schema. Property names must be exactly as in the schema (camelCase where shown)." },
                        new { role = "user", content = _question + "\n\nCV text:\n" + prompt + "\n\nReturn JSON matching this schema:\n" + schema }
                    },
                    max_completion_tokens = 20000
                };

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var json = JsonSerializer.Serialize(requestBody, options);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                const int maxRetries = 3;
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    _logger.LogInformation("Sending request to OpenAI (Attempt {Attempt}/{MaxRetries})", attempt + 1, maxRetries);

                    var response = await _httpClient.PostAsync(_completionUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        _logger.LogError(
                            "OpenAI request failed. Status: {StatusCode} ({ReasonPhrase}). Body: {Body}",
                            (int)response.StatusCode,
                            response.ReasonPhrase,
                            errorBody
                        );

                        if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxRetries - 1)
                        {
                            var delayMs = 1000 * (attempt + 1);
                            _logger.LogWarning("Rate limit hit. Retrying in {Delay}ms...", delayMs);
                            await Task.Delay(delayMs);
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                    }

                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("OpenAI raw response: {Response}", responseString);

                    using var jsonDoc = JsonDocument.Parse(responseString);

                    if (!jsonDoc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    {
                        throw new Exception("Invalid OpenAI response: no choices returned.");
                    }

                    return choices[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString()
                        ?? throw new Exception("OpenAI returned an empty content field.");
                }

                throw new Exception("Max retries reached without success.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating content from OpenAI. Inner: {Inner}", ex.InnerException?.Message);
                throw new Exception("Error generating content from OpenAI", ex);
            }
        }


        // Sends CV text to OpenAI and returns the parsed JSON string
        public async Task<string> SendCvAsync(string cvText)
        {
            return await GenerateContentAsync(cvText);
        }


        // Read an embedded resource file from any loaded assembly
        private async Task<string> ReadEmbeddedResourceAsync(string resourceName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var resourceNames = assembly.GetManifestResourceNames();

                var match = resourceNames
                    .FirstOrDefault(r => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    _logger.LogInformation("Loading embedded resource: {ResourceName} from {Assembly}", match, assembly.FullName);

                    using (var stream = assembly.GetManifestResourceStream(match))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
            }

            throw new FileNotFoundException(
                $"Embedded resource ending with '{resourceName}' was not found in any loaded assemblies."
            );
        }
    }
}
