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

    // In-memory cache for uploded CV:n
    private static readonly ConcurrentDictionary<string, Cv> _cvCache = new();

    public CvController(
        ICvParserService cvParserService,
        ICvDocumentGenerator cvDocumentGenerator,
        ILogger<CvController> logger)
    {
        _cvParserService = cvParserService;
        _cvDocumentGenerator = cvDocumentGenerator;
        _logger = logger;
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
        _logger.LogInformation("CV extraherat från filen {FileName}", file.FileName);

        // Unique token/ID for this CV
        var cvToken = System.Guid.NewGuid().ToString();
        _cvCache[cvToken] = cv;

        // Return token to client
        return Ok(ApiResponse<string>.SuccessResponse(cvToken));
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string token, [FromQuery] string format = "word", [FromQuery] string lang = "sv")
    {
        if (string.IsNullOrEmpty(token) || !_cvCache.TryGetValue(token, out var cv))
            return BadRequest("CV hittades inte. Ladda upp först.");

        var docxResult = await _cvDocumentGenerator.CreateXameraCVAsync(cv, locale: lang);
        if (docxResult.IsFailure)
            return StatusCode(500, $"Internt fel: {docxResult.Error}");

        var docxStream = docxResult.Value!;
        docxStream.Position = 0;

        var safeName = string.Concat((cv.PersonalInfo?.Name ?? "CV").Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        var fileName = $"{safeName}.{(format == "pdf" ? "pdf" : "docx")}";

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdfResult = await _cvDocumentGenerator.ConvertDocxToPdfAsync(docxStream);
            if (pdfResult.IsFailure || pdfResult.Value == null)
                return StatusCode(500, $"Internt fel: {pdfResult.Error ?? "PDF-konvertering misslyckades"}");

            var pdfStream = pdfResult.Value!;
            pdfStream.Position = 0;
            return File(pdfStream, "application/pdf", fileName);
        }

        return File(docxStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }
}


/*
using System;
using System.IO;
using System.Drawing;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using CvParser.Domain.Constants.ResponseMessages;
using CvParser.Domain.Entities.CV.V1;
using CvParser.Domain.Enums;
using CvParser.Infrastructure.Interfaces;
using CvParser.Infrastructure.Services;
using Microsoft.AspNetCore.Antiforgery;
using CvParser.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Net;


namespace CvParser.Web.Controllers;


[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]
public class CvController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IPdfService _pdfService;
    private readonly IDocxService _docxService;
    private readonly IImageService _imageService;
    private readonly ICvService _cvService;
    private readonly ISessionStorageService _sessionStorageService;

    public CvController(
        IAiService aiService,
        IPdfService pdfService,
        IDocxService docxService,
        IImageService imageService,
        ICvService cvService, 
        ISessionStorageService sessionStorageService)
    {
        _aiService = aiService;
        _pdfService = pdfService;
        _docxService = docxService;
        _imageService = imageService;
        _cvService = cvService;
        _sessionStorageService = sessionStorageService;
    }

    [HttpGet("antiforgery-token")]
    public IActionResult GetAntiforgeryToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [HttpGet("ai-test")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> TestAi([FromServices] IAiService aiService)
    {
        try
        {
            var testText = "This is a test CV text.";
            var resultJson = await aiService.SendCvAsync(testText);

        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return NoContent();
        }

        return Ok(resultJson);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"OpenAI error: {ex}");
        }
    }

    [HttpPost("send")]
    public async Task<IActionResult> CreateModel(IFormFile file)
    {
        if (file == null)
            return BadRequest(CvResponseMessages.NoFileUploaded);

        try
        {
            var cv = await _cvService.ParseCvFromUploadedFileAsync(file);
            if (cv == null)
                return NoContent();

            return Ok(cv);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"{AppResponseMessages.InternalServerError} {ex.Message}");
        }
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateNewXameraCv([FromBody] Cv cv)
    {
        try
        {
            var documentStream = await _cvService.CreateXameraCV(cv);
            documentStream.Position = 0;

            return File(documentStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "");

        }
        catch (Exception ex)
        {
            return StatusCode(500, $"{AppResponseMessages.InternalServerError} {ex} {ex.StackTrace} {ex.InnerException}");
        }
    }
}
*/
