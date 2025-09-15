using CvParser.Domain.Entities.CV.V1;
using CvParser.Domain.Common;
using System.Collections.Concurrent;

[ApiController]
[Route("api/[controller]")]
public class CvController : ControllerBase
{
    private readonly ICvParserService _cvParserService;
    private readonly ICvDocumentGenerator _cvDocumentGenerator;
    private readonly ILogger<CvController> _logger;
    private readonly IConfiguration _config;

    // In-memory cache for uploded CV:n
    private static readonly ConcurrentDictionary<string, Cv> _cvCache = new();

    public CvController(
        ICvParserService cvParserService,
        ICvDocumentGenerator cvDocumentGenerator,
        ILogger<CvController> logger,
        IConfiguration config)
    {
        _cvParserService = cvParserService;
        _cvDocumentGenerator = cvDocumentGenerator;
        _logger = logger;
        _config = config;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadCv([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<string>.FailureResponse("Ingen fil uppladdad."));

        var result = await _cvParserService.ParseCvFromUploadedFileAsync(file);

        if (result.IsFailure)
        {
            _logger.LogWarning("Kunde inte tolka CV från fil {FileName}: {Error}", file.FileName, result.Error);
            return StatusCode(500, ApiResponse<string>.FailureResponse(result.Error ?? "Internt fel."));
        }

        var cv = result.Value!;
        _logger.LogInformation("CV extraherat från filen {FileName}, locale: {locale}", file.FileName, cv.Locale);

        // Unique token/ID for this CV
        var cvToken = System.Guid.NewGuid().ToString();
        _cvCache[cvToken] = cv;

        // Return token to client
        return Ok(ApiResponse<string>.SuccessResponse(cvToken));
    }


    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string token, [FromQuery] string format = "word")
    {
        if (string.IsNullOrEmpty(token) || !_cvCache.TryGetValue(token, out var cv))
            return BadRequest("CV hittades inte. Ladda upp först.");


        var locale = string.IsNullOrWhiteSpace(cv.Locale) ? "sv" : cv.Locale.ToLower();
        var templatePath = locale == "en"
            ? _config["CvServiceSettings:CvTemplatePathEn"]
            : _config["CvServiceSettings:CvTemplatePathSv"];

        var docxResult = await _cvDocumentGenerator.CreateXameraCVAsync(cv, locale: locale);

        if (docxResult.IsFailure)
            return StatusCode(500, $"Internt fel: {docxResult.Error}");

        var docxStream = docxResult.Value!;
        docxStream.Position = 0;

        var safeName = string.Concat((cv.PersonalInfo?.Name ?? "XameraCV")
            .Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        var fileName = $"{safeName}.docx";

        return File(docxStream,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }
}
