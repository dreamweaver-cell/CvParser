using Microsoft.Extensions.Logging;
using CvParser.Domain.Entities.CV.V1;
using CvParser.Domain.Common;

namespace CvParser.Infrastructure.Services
{
    public class CvParserService : ICvParserService
    {
        private readonly IAiService _aiService;
        private readonly IImageService _imageService;
        private readonly ILogger<CvParserService> _logger;

        public CvParserService(IAiService aiService, IImageService imageService, ILogger<CvParserService> logger)
        {
            _aiService = aiService;
            _imageService = imageService;
            _logger = logger;
        }


        // Wrapper for parsing CV from uploaded file
        public async Task<Result<Cv?>> ParseCvFromUploadedFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Result<Cv?>.Failure("Ingen fil laddades upp.");
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".pdf" && ext != ".docx")
                return Result<Cv?>.Failure($"Filformatet '{ext}' stöds inte.");
            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                // Extrahera text
                ms.Position = 0;
                string text = _imageService.ExtractTextFromDocument(ms, ext);
                if (string.IsNullOrWhiteSpace(text))
                    return Result<Cv?>.Failure("Kunde inte extrahera text från dokumentet.");
                // Ansiktskontroll
                ms.Position = 0;
                bool hasFace = await _imageService.HasHumanFaceInDocumentAsync(ms, ext);
                // AI-parsing
                var json = await _aiService.SendCvAsync(text);
                if (string.IsNullOrWhiteSpace(json))
                    return Result<Cv?>.Failure("AI-tjänsten returnerade inget innehåll.");
                var cv = JsonSerializer.Deserialize<Cv>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (cv == null)
                    return Result<Cv?>.Failure("Kunde inte deserialisera AI-svar till Cv.");
                if (hasFace)
                {
                    ms.Position = 0;
                    var images = ext == ".pdf"
                        ? await _imageService.ExtractImagesFromPdfAsync(ms)
                        : await _imageService.ExtractImagesFromDocxAsync(ms);
                    foreach (var img in images)
                    {
                        if (_imageService.HasHumanFace(img))
                        {
                            cv.PersonalInfo.ImageBase64 = await _imageService.ToBase64Async(img);
                            break;
                        }
                    }
                }
                return Result<Cv?>.Success(cv);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid parsning av dokumentet.");
                return Result<Cv?>.Failure("Ett internt fel inträffade vid parsning.");
            }
        }
    }
}
