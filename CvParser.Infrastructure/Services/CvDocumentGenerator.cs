using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CvParser.Domain.Entities.CV.V1;
using CvParser.Domain.Common;
using DW = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using DWP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using WPDrawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;


namespace CvParser.Infrastructure.Services
{
    public class CvDocumentGenerator : ICvDocumentGenerator
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CvDocumentGenerator> _logger;
        private readonly IImageService _imageService;
        private readonly string _templatePathSv;
        private readonly string _templatePathEn;
        private readonly string _defaultLocale;

        public CvDocumentGenerator(
            IConfiguration configuration,
            IWebHostEnvironment env,
            ILogger<CvDocumentGenerator> logger,
            IImageService imageService)
        {
            _templatePathSv = configuration["CvServiceSettings:CvTemplatePathSv"] ?? string.Empty;
            _templatePathEn = configuration["CvServiceSettings:CvTemplatePathEn"] ?? string.Empty;
            _defaultLocale = configuration["CvServiceSettings:DefaultLocale"]; 
            _env = env;
            _logger = logger;
            _imageService = imageService;
        }

        public async Task<Result<MemoryStream>> CreateXameraCVAsync(Cv cv, string locale)
        {
            try
            {
                var templateFileRel = PickTemplateByLocale(locale);
                var templateFile = Path.Combine(_env.ContentRootPath, templateFileRel);
                _logger.LogInformation("Using CV template at {TemplateFile} (locale={Locale})", templateFile, locale);

                if (!File.Exists(templateFile))
                {
                    var fallbackRel = PickTemplateByLocale(_defaultLocale);
                    var fallbackAbs = Path.Combine(_env.ContentRootPath, fallbackRel);
                    if (!File.Exists(fallbackAbs))
                        return Result<MemoryStream>.Failure($"Template not found (locale={locale}) nor default at {fallbackAbs}");
                    templateFile = fallbackAbs;
                }

                using var templateFileStream = File.OpenRead(templateFile);
                var resultStream = new MemoryStream();
                await templateFileStream.CopyToAsync(resultStream);
                resultStream.Position = 0;

                using (var doc = WordprocessingDocument.Open(resultStream, true))
                {
                    if (doc.MainDocumentPart?.Document == null)
                        return Result<MemoryStream>.Failure("Template saknar MainDocumentPart eller Document.");

                    // Log summary of parsed CV for diagnostics
                    _logger.LogInformation(
                        "CV input — Name:'{Name}', Title:'{Title}', Summary:{HasSummary}, Edu:{EduCount}, Work:{WorkCount}, Employers:{EmpCount}, Skills:{SkillCount}, Certs:{CertCount}, Langs:{LangCount}",
                        cv.PersonalInfo?.Name,
                        cv.PersonalInfo?.Title,
                        string.IsNullOrWhiteSpace(cv.Summary) ? "No" : "Yes",
                        cv.Educations?.Count ?? 0,
                        cv.WorkExperiences?.Count ?? 0,
                        cv.Employers?.Count ?? 0,
                        cv.Competencies?.Count ?? 0,
                        cv.Certificates?.Count ?? 0,
                        cv.Languages?.Count ?? 0
                    );

                    var replacements = BuildReplacementMap(cv);

                    // Check for missing placeholders in the template (to avoid silent failures)
                    var missing = FindMissingPlaceholders(doc.MainDocumentPart, replacements.Keys);
                    
                    if (missing.Count > 0)
                    {
                        _logger.LogWarning("Följande placeholders saknas i templaten: {Missing}", string.Join(", ", missing));
                    }

                    ReplaceTextPlaceholders(doc.MainDocumentPart, replacements);

                    ReplacePhotoPreservingStyle(doc, "|photo|", cv.PersonalInfo?.ImageBase64);
                    doc.MainDocumentPart.Document.Save();
                }

                resultStream.Position = 0;
                return Result<MemoryStream>.Success(resultStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid skapande av CV.");
                return Result<MemoryStream>.Failure($"Ett fel uppstod: {ex.Message}");
            }
        }

        private string PickTemplateByLocale(string? locale)
            => string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase)
                ? _templatePathEn
                : _templatePathSv;

        
        public async Task<Result<MemoryStream?>> ConvertDocxToPdfAsync(MemoryStream docxStream)
        {
            try
            {
                return Result<MemoryStream?>.Failure("PDF-konvertering ej implementerad i detta utdrag.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid konvertering av DOCX till PDF.");
                return Result<MemoryStream?>.Failure($"Fel vid konvertering till PDF: {ex.Message}");
            }
        }

        private Dictionary<string, string> BuildReplacementMap(Cv cv)
        {
            var name = cv.PersonalInfo?.Name ?? string.Empty;
            var title = cv.PersonalInfo?.Title ?? string.Empty; 
            var summary = cv.Summary ?? string.Empty;

            var educations = RenderEducations(cv.Educations);
            var workExperiences = RenderWorkExperiences(cv.WorkExperiences);
            var employers = RenderEmployers(cv.Employers);
            var competencies = RenderCompetencies(cv.Competencies);
            var certificates = RenderCertificates(cv.Certificates);
            var languages = RenderLanguages(cv.Languages);
            var other = RenderOther(cv.Other);

            // Placeholders in the word template are like |name|, |educations| etc
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Personal info
                ["|name|"] = name,
                ["|title|"] = title,
                ["|summary|"] = summary,

                // Lists/sections
                ["|educations|"] = educations,
                ["|workexperiences|"] = workExperiences,
                ["|employers|"] = employers,
                ["|competencies|"] = competencies,
                ["|certificates|"] = certificates,
                ["|languages|"] = languages,
                ["|other|"] = other,
            };

            return map;
        }

        private static string RenderEducations(List<Education>? items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var e in items)
            {
                var line = $"{NV(e.Title) ?? NV(e.Degree)} – {NV(e.Institution)}";
                var period = CompactJoin(" | ", $"{NV(e.StartDate)}–{NV(e.EndDate)}", NV(e.Period));
                if (!string.IsNullOrWhiteSpace(period)) line += $" ({period})";
                sb.AppendLine("• " + line.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        private static string RenderWorkExperiences(List<WorkExperience>? items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var w in items)
            {
                var head = $"{NV(w.Position)} – {NV(w.CompanyName)}";
                var dates = $"{NV(w.StartDate)}–{NV(w.EndDate)}".Trim('-');
                if (!string.IsNullOrWhiteSpace(dates)) head += $" ({dates})";

                sb.AppendLine("• " + head.Trim());

                if (!string.IsNullOrWhiteSpace(w.Description))
                    sb.AppendLine($"  {w.Description.Trim()}");

                if (!string.IsNullOrWhiteSpace(w.Technologies))
                    sb.AppendLine($"  Teknik: {w.Technologies.Trim()}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string RenderEmployers(List<Employer>? items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var e in items)
            {
                var line = $"{NV(e.Company)} – {NV(e.Position)}";
                if (!string.IsNullOrWhiteSpace(e.Period)) line += $" ({e.Period.Trim()})";
                sb.AppendLine("• " + line.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        private static string RenderCompetencies(List<Competency>? items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in items)
            {
                var line = c.Name;
                if (!string.IsNullOrWhiteSpace(c.Level)) line += $" ({c.Level})";
                if (!string.IsNullOrWhiteSpace(c.SkillCategory)) line += $" – {c.SkillCategory}";
                if (c.IsMainSkill) line += " [Huvudkompetens]";
                sb.AppendLine("• " + line.Trim());

                if (c.Keywords != null && c.Keywords.Count > 0)
                    sb.AppendLine("  Nyckelord: " + string.Join(", ", c.Keywords));
            }
            return sb.ToString().TrimEnd();
        }

        private static string RenderCertificates(List<Certificate>? items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in items)
            {
                var line = NV(c.Name) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(c.Institution))
                    line += $" – {c.Institution.Trim()}";
                sb.AppendLine("• " + line.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        private static string RenderLanguages(List<Language>? items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var l in items)
            {
                var line = $"{NV(l.LanguageName)}";
                if (!string.IsNullOrWhiteSpace(l.Level)) line += $" – {l.Level.Trim()}";
                if (!string.IsNullOrWhiteSpace(l.Fluency)) line += $" ({l.Fluency.Trim()})";
                sb.AppendLine("• " + line.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        private static string RenderOther(List<string?>? items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var s in items.Where(x => !string.IsNullOrWhiteSpace(x)))
                sb.AppendLine("• " + s!.Trim());
            return sb.ToString().TrimEnd();
        }

        private static string NV(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        private static string CompactJoin(string sep, params string?[] parts)
            => string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

        private void ReplaceTextPlaceholders(MainDocumentPart main, Dictionary<string, string> replacements)
        {
            foreach (var para in main.Document.Body.Descendants<Paragraph>())
            {
                var texts = para.Descendants<Text>().ToList();
                if (texts.Count == 0) continue;

                var full = string.Concat(texts.Select(t => t.Text ?? string.Empty));
                if (full.IndexOf('|') < 0) continue; 

                foreach (var kvp in replacements)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        full = ReplaceOrdinalIgnoreCase(full, kvp.Key, kvp.Value ?? string.Empty);
                    }
                }

                // Important: write back without erase runs → preservs images/ankare/hyperlinks/styles
                texts[0].Text = full;
                texts[0].Space = SpaceProcessingModeValues.Preserve; // keep i.e double space/linebreaks
                for (int i = 1; i < texts.Count; i++)
                    texts[i].Text = string.Empty;
            }
        }

        private static string ReplaceOrdinalIgnoreCase(string input, string search, string replacement)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(search)) return input;
            var sb = new StringBuilder(input.Length);
            int i = 0;
            while (i < input.Length)
            {
                int idx = input.IndexOf(search, i, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    sb.Append(input, i, input.Length - i);
                    break;
                }
                sb.Append(input, i, idx - i);
                sb.Append(replacement);
                i = idx + search.Length;
            }
            return sb.ToString();
        }

        // Check which placeholders are missing in the document Template
        private List<string> FindMissingPlaceholders(MainDocumentPart main, IEnumerable<string> keys)
        {
            // Combine all text in the document
            var docText = string.Concat(
                main.Document.Descendants<Text>()
                    .Select(t => t.Text ?? string.Empty));

            var missing = new List<string>();

            foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (docText.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0)
                    missing.Add(key);
            }

            return missing;
        }

        // Try to replace an existing image with alt-text containing the marker (e.g. |photo|)
        private void ReplacePhotoPreservingStyle(WordprocessingDocument doc, string marker, string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return;

            var main = doc.MainDocumentPart!;
            var replaced = ReplaceImageDataByAltText(main, marker, base64);
            if (!replaced)
            {
                // Fallback: replace by inserting new image at placeholder paragraph
                InsertOrReplaceImageAtPlaceholder(doc, marker.Trim('|'), base64);
            }
        }

        // Try to find an image with alt-text containing the marker (e.g. |photo|) and replace its data
        private bool ReplaceImageDataByAltText(MainDocumentPart main, string marker, string base64Image)
        {
            bool Match(string? s) =>
                !string.IsNullOrEmpty(s) &&
                s.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;

            // Find all drawings in the document
            foreach (var drawing in main.Document.Body.Descendants<WPDrawing>())
            {
                // Find the Picture element inside the drawing
                var pic = drawing.Descendants<PIC.Picture>().FirstOrDefault();
                if (pic == null) continue;

                // 1) Non-visual props on the Picture itself (may be missing) – sometimes alt-text is here
                var nv = pic.NonVisualPictureProperties?.NonVisualDrawingProperties;
                var name = nv?.Name?.Value;
                var descr = nv?.Description?.Value;

                // 2) DocProperties on the Drawing (may be missing) – sometimes alt-text is here
                var dp = drawing.Descendants<DWP.DocProperties>().FirstOrDefault();
                var dpName = dp?.Name?.Value;
                var dpDescr = dp?.Description?.Value;

                if (!(Match(name) || Match(descr) || Match(dpName) || Match(dpDescr)))
                    continue;

                // Relation to the image part
                var blip = pic.BlipFill?.Blip;
                if (blip?.Embed == null) continue;

                var imagePart = main.GetPartById(blip.Embed) as ImagePart;
                if (imagePart == null) continue;

                // Replace image data while preserving the ImagePart (and thus all styles/effects)
                byte[] pngBytes;
                try
                {
                    if (!_imageService.TryPrepareInlinePng(base64Image, maxWidthPx: 600, maxHeightPx: 600,
                        out pngBytes, out _, out _, out _, out _))
                    {
                        pngBytes = Convert.FromBase64String(base64Image);
                    }
                }
                catch
                {
                    pngBytes = Convert.FromBase64String(base64Image);
                }

                using var ms = new MemoryStream(pngBytes);
                imagePart.FeedData(ms); // Change image data

                return true;
            }

            return false;
        }

        // Insert a new image at the paragraph containing the placeholder (e.g. |photo|)
        // Replaces all runs in that paragraph with a single run containing the image
        private void InsertOrReplaceImageAtPlaceholder(WordprocessingDocument doc, string placeholderName, string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return;

            var main = doc.MainDocumentPart!;
            var para = main.Document.Body
                .Descendants<Paragraph>()
                .FirstOrDefault(p => p.InnerText.IndexOf($"|{placeholderName}|", StringComparison.OrdinalIgnoreCase) >= 0);

            if (para == null) return;

            para.RemoveAllChildren<Run>();

            var drawing = CreateInlineImage(main, base64, maxWidthPx: 180, maxHeightPx: 20);
            var run = new Run(drawing);
            para.Append(run);
        }

        // Create a Drawing element for an inline image from Base64 string
        // Resizes image to fit within maxWidthPx x maxHeightPx while preserving aspect ratio
        private WPDrawing CreateInlineImage(MainDocumentPart mainPart, string base64Image, int maxWidthPx, int maxHeightPx)
        {
            if (!_imageService.TryPrepareInlinePng(base64Image, maxWidthPx, maxHeightPx,
                out var pngBytes, out var widthPx, out var heightPx, out var dpiX, out var dpiY))
            {
                throw new ArgumentException("Ogiltig bild (Base64) eller kunde inte förbereda PNG.");
            }

            long widthEmu  = PxToEmu(widthPx,  dpiX);
            long heightEmu = PxToEmu(heightPx, dpiY);

            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(pngBytes))
                imagePart.FeedData(ms);
            string relId = mainPart.GetIdOfPart(imagePart);

            return new WPDrawing(
                new DWP.Inline(
                    new DWP.Extent() { Cx = widthEmu, Cy = heightEmu },
                    new DWP.DocProperties() { Id = 1U, Name = "Image" },
                    new DW.Graphic(
                        new DW.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "Image" },
                                    new PIC.NonVisualPictureDrawingProperties()
                                ),
                                new PIC.BlipFill(
                                    new DW.Blip() { Embed = relId },
                                    new DW.Stretch(new DW.FillRectangle())
                                ),
                                new PIC.ShapeProperties(
                                    new DW.Transform2D(
                                        new DW.Offset() { X = 0, Y = 0 },
                                        new DW.Extents() { Cx = widthEmu, Cy = heightEmu }
                                    ),
                                    new DW.PresetGeometry(new DW.AdjustValueList()) { Preset = DW.ShapeTypeValues.Rectangle }
                                )
                            )
                        ){ Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                    )
                )
            );
        }

        // Convert pixels to EMUs (English Metric Units) used by OpenXML
        // EMU = inches * 914400; inches = pixels / dpi
        private static long PxToEmu(int px, int dpi)
        {
            double inches = px / Math.Max(dpi, 1.0);
            return (long)Math.Round(inches * 914400.0);
        }
    }
}
