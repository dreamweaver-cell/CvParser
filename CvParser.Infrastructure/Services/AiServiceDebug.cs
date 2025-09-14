using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using CvParser.Infrastructure.Interfaces;

namespace CvParser.Infrastructure.Services.Debug
{
    public class AiServiceDebug : IAiService
    {
        private readonly IAiService _inner;
        private readonly ILogger<AiServiceDebug> _logger;

        public AiServiceDebug(IAiService inner, ILogger<AiServiceDebug> logger)
        {
            _inner = inner;
            _logger = logger;
        }


        public async Task<string> GenerateContentAsync(string prompt)
        {
            _logger.LogInformation("DEBUG: Genererar innehåll med prompt: {Prompt}", prompt);

            try
            {
                var result = await _inner.GenerateContentAsync(prompt);
                _logger.LogInformation("DEBUG: AI genererade: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: Fel vid GenerateContentAsync.");
                throw;
            }
        }

        public async Task<string> SendCvAsync(string cvText)
        {
            _logger.LogInformation("DEBUG: Skickar CV-text till AI: {CvText}", cvText);

            try
            {
                var result = await _inner.SendCvAsync(cvText);
                _logger.LogInformation("DEBUG: AI svarade med: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: Fel vid SendCvAsync.");
                throw;
            }
        }
    }
}
