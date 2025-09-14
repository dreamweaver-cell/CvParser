using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using CvParser.Domain.Entities.CV.V1;
using CvParser.Infrastructure.Interfaces;
using CvParser.Domain.Common;

namespace CvParser.Infrastructure.Services
{
    public class CvService : ICvService
    {
        private readonly ICvParserService _parserService;

        public CvService(
            ICvParserService parserService)
        {
            _parserService = parserService;
        }

        public Task<Result<Cv?>> ParseCvAsync(IFormFile file)
        {
            return _parserService.ParseCvFromUploadedFileAsync(file);
        }
    }
}






/*
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CvParser.Domain.Entities.CV.V1;
using CvParser.Infrastructure.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using Break = DocumentFormat.OpenXml.Wordprocessing.Break;
using Drawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using FontSize = DocumentFormat.OpenXml.Wordprocessing.FontSize;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;

namespace CvParser.Infrastructure.Services
{
    public class CvService : ICvService
    {
        private readonly string _cvTemplatePath;
        private readonly IDocxService _docxService;
        private readonly IPdfService _pdfService;
        private readonly IImageService _imageService;
        private readonly IAiService _aiService;
        private readonly ILogger<CvService> _logger;
        private readonly IWebHostEnvironment _env;

        public CvService(
            IConfiguration configuration,
            IDocxService docxService,
            IPdfService pdfService,
            IImageService imageService,
            IAiService aiService,
            ILogger<CvService> logger,
            IWebHostEnvironment env)
        {
            _cvTemplatePath = configuration["CvServiceSettings:CvTemplatePath"]
                ?? throw new InvalidOperationException("CV template path is not configured.");

            _docxService = docxService;
            _pdfService = pdfService;
            _imageService = imageService;
            _aiService = aiService;
            _logger = logger;
            _env = env;
        }

        public async Task<Cv?> ParseCvFromUploadedFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Ingen fil laddades upp eller filen var tom.");
                return null;
            }

            try
            {
                string documentText = string.Empty;
                var documentImages = new List<System.Drawing.Image>();

                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    bool isPdf = file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

                    if (isPdf)
                    {
                        documentText = _pdfService.GetText(memoryStream);
                        memoryStream.Position = 0;
                        documentImages = _pdfService.GetImages(memoryStream);
                    }
                    else
                    {
                        documentText = _docxService.GetText(memoryStream);
                        memoryStream.Position = 0;
                        documentImages = _docxService.GetImages(memoryStream);
                    }
                }

                if (string.IsNullOrWhiteSpace(documentText))
                {
                    _logger.LogInformation("Ingen text kunde extraheras från dokumentet.");
                    return null;
                }

                var resultJson = await _aiService.SendCvAsync(documentText);

                if (string.IsNullOrWhiteSpace(resultJson))
                {
                    _logger.LogInformation("AI-tjänsten returnerade ingen JSON-data.");
                    return null;
                }

                Cv? cv = JsonSerializer.Deserialize<Cv>(resultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (cv != null && documentImages.Any())
                {
                    foreach (var img in documentImages)
                    {
                        if (_imageService.HasHumanFace(img))
                        {
                            cv.PersonalInfo.ImageBase64 = await _imageService.ToBase64Async(img);
                            break;
                        }
                    }
                }

                return cv;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ett fel uppstod vid parsning av den uppladdade filen.");
                throw;
            }
        }

        public async Task<MemoryStream> CreateXameraCV(Cv cv)
        {
            string templateFile = Path.Combine(_env.ContentRootPath, _cvTemplatePath);

            if (!File.Exists(templateFile))
                throw new FileNotFoundException($"CV template file not found at: {templateFile}");

            using var templateFileStream = File.OpenRead(templateFile);
            var resultStream = new MemoryStream();
            await templateFileStream.CopyToAsync(resultStream);
            resultStream.Position = 0;

            using (var doc = WordprocessingDocument.Open(resultStream, true))
            {
                if (doc.MainDocumentPart == null || doc.MainDocumentPart.Document == null)
                    throw new InvalidOperationException("Template saknar MainDocumentPart eller Document.");

                CreateFirstPage(doc, cv);
                NewPage(doc);
                AddAllSkills(doc, cv.Competencies);
                AddProjects(doc, cv.WorkProjects);
                AddWork(doc, cv.Employers);
            }

            resultStream.Position = 0;
            return resultStream;
        }

        private void CreateFirstPage(WordprocessingDocument doc, Cv cv)
        {
            var allTexts = GetAllTextElements(doc);

            foreach (var textElement in allTexts)
            {
                switch (textElement.Text)
                {
                    case "|Name|": textElement.Text = cv.PersonalInfo.Name; break;
                    case "|Title|": textElement.Text = cv.PersonalInfo.Title; break;
                    case "|Summary|": textElement.Text = cv.Summary; break;
                    case "|MainSkills|": ReplaceMainSkills(doc, cv.Competencies, textElement); break;
                    case "|Educations|": ReplaceEducations(doc, cv.Educations, textElement); break;
                    case "|Languages|": ReplaceLanguages(doc, cv.Languages, textElement); break;
                }
            }

            ReplaceProfilePicture(doc, cv.PersonalInfo.ImageBase64);
        }

        private IEnumerable<Text> GetAllTextElements(WordprocessingDocument doc)
        {
            var texts = new List<Text>();

            if (doc.MainDocumentPart?.Document?.Body != null)
                texts.AddRange(doc.MainDocumentPart.Document.Body.Descendants<Text>());

            foreach (var header in doc.MainDocumentPart.HeaderParts)
                texts.AddRange(header.RootElement.Descendants<Text>());

            foreach (var footer in doc.MainDocumentPart.FooterParts)
                texts.AddRange(footer.RootElement.Descendants<Text>());

            foreach (var drawing in doc.MainDocumentPart.Document.Descendants<Drawing>())
                texts.AddRange(drawing.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => new Text(t.Text)));

            return texts;
        }

        private void NewPage(WordprocessingDocument doc)
        {
            var body = doc.MainDocumentPart.Document.Body;
            var pageBreakParagraph = new Paragraph();
            var run = new Run(new Break() { Type = BreakValues.Page });
            pageBreakParagraph.Append(run);
            body.Append(pageBreakParagraph);
        }

        private void ReplaceProfilePicture(WordprocessingDocument doc, string? imageBase64)
        {
            foreach (var imagePart in doc.MainDocumentPart.ImageParts)
            {
                if (!string.IsNullOrEmpty(imageBase64))
                {
                    byte[] imageBytes = Convert.FromBase64String(imageBase64);
                    using (var ms = new MemoryStream(imageBytes))
                        imagePart.FeedData(ms);
                }
                else
                {
                    var drawings = doc.MainDocumentPart.RootElement.Descendants<Drawing>()
                        .Where(d => d.Descendants<WP.Inline>().Any(p => p.Descendants<DW.Blip>()
                        .Any(b => b.Embed == doc.MainDocumentPart.GetIdOfPart(imagePart)))).ToList();

                    foreach (var d in drawings) d.Remove();
                    doc.MainDocumentPart.DeleteParts(new List<ImagePart>() { imagePart });
                }
            }
        }

        private void ReplaceMainSkills(WordprocessingDocument doc, List<Competency>? competencies, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;
            parentParagraph.RemoveChild(parentRun);

            if (competencies == null || !competencies.Any()) return;

            foreach (var skill in competencies.Where(c => c.IsMainSkill))
            {
                var competencyRun = CreateFormattedRun(skill.Name, "20");
                parentParagraph.AppendChild(competencyRun);

                foreach (var keyword in skill.Keywords ?? Enumerable.Empty<string>())
                    parentParagraph.AppendChild(CreateFormattedRun(keyword, "20"));

                parentParagraph.AppendChild(new Run(new Break()));
            }
        }

        private void ReplaceEducations(WordprocessingDocument doc, List<Education>? educations, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;
            parentParagraph.RemoveChild(parentRun);

            if (educations == null || !educations.Any()) return;

            foreach (var edu in educations)
            {
                var years = $"{edu.StartDate} - {edu.EndDate}";
                parentParagraph.AppendChild(CreateFormattedRun(years, "20", true));
                parentParagraph.AppendChild(new Run(new Break()));
                parentParagraph.AppendChild(CreateFormattedRun(edu.Institution, "20"));
                parentParagraph.AppendChild(new Run(new Break()));
                parentParagraph.AppendChild(CreateFormattedRun(edu.StudyType, "18"));
                parentParagraph.AppendChild(new Run(new Break()));
            }
        }

        private void ReplaceLanguages(WordprocessingDocument doc, List<Language>? languages, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;
            parentParagraph.RemoveChild(parentRun);

            if (languages == null || !languages.Any()) return;

            bool first = true;
            foreach (var lang in languages)
            {
                if (!first) parentParagraph.AppendChild(new Run(new Break()));
                first = false;
                parentParagraph.AppendChild(CreateFormattedRun($"{lang.LanguageName}: {lang.Fluency}", "20"));
            }
        }

        private Run CreateFormattedRun(string text, string fontSize, bool isBold = false)
        {
            var runProps = new RunProperties(new RunFonts() { Ascii = "Albert Sans" }, new FontSize() { Val = fontSize });
            if (isBold) runProps.Append(new Bold());
            return new Run(runProps, new Text(text));
        }

        private void AddAllSkills(WordprocessingDocument doc, List<Competency>? competencies)
        {
            if (competencies == null || !competencies.Any()) return;

            AddHeader(doc, "Skills");
            var body = doc.MainDocumentPart.Document.Body;

            foreach (var competency in competencies)
            {
                var table = new Table();
                var tblProps = new TableProperties(new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None }),
                    new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" });
                table.AppendChild(tblProps);
                table.AppendChild(new TableGrid(new GridColumn() { Width = "2500" }, new GridColumn() { Width = "2500" }));

                var headerRow = new TableRow();
                var runHeader = new Run(new RunProperties(new RunFonts() { Ascii = "Albert Sans" }, new FontSize() { Val = "24" }, new Bold()), new Text(competency.SkillCategory ?? ""));
                var paraHeader = new Paragraph(runHeader);
                headerRow.Append(new TableCell(paraHeader));
                headerRow.Append(new TableCell(new Paragraph(new Run(new Text(string.Empty)))));
                table.Append(headerRow);

                for (int i = 0; i < competency.Keywords.Count; i += 2)
                {
                    var tr = new TableRow();
                    tr.Append(CreateCell(competency.Keywords[i]));
                    if (i + 1 < competency.Keywords.Count) tr.Append(CreateCell(competency.Keywords[i + 1]));
                    table.Append(tr);
                }

                body.Append(table);
            }
        }

        private TableCell CreateCell(string text)
        {
            var run = new Run(new RunProperties(new RunFonts() { Ascii = "Albert Sans" }, new FontSize() { Val = "20" }), new Text(text));
            var para = new Paragraph(run);
            var cell = new TableCell(para);
            cell.Append(new TableCellProperties(new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None })));
            return cell;
        }

        private void AddProjects(WordprocessingDocument doc, List<WorkProject>? projects)
        {
            if (projects == null || !projects.Any()) return;
            NewPage(doc);
            AddHeader(doc, "Projects");
        }

        private void AddWork(WordprocessingDocument doc, List<Employer>? works)
        {
            if (works == null || !works.Any()) return;
            NewPage(doc);
            AddHeader(doc, "Work");
        }

        private void AddHeader(WordprocessingDocument doc, string name)
        {
            var runProps = new RunProperties(new RunFonts() { Ascii = "Albert Sans" }, new FontSize() { Val = "32" }, new Bold());
            var run = new Run(runProps, new Text(name));
            var para = new Paragraph(run);
            doc.MainDocumentPart.Document.Body.Append(para);
        }
    }
}
*/


/*
using Microsoft.Extensions.Configuration;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using DW = DocumentFormat.OpenXml.Drawing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;
using Break = DocumentFormat.OpenXml.Wordprocessing.Break;
using Drawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using FontSize = DocumentFormat.OpenXml.Wordprocessing.FontSize;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using CvParser.Domain.Entities.CV.V1;
using Cv = CvParser.Domain.Entities.CV.V1.Cv;
using Education = CvParser.Domain.Entities.CV.V1.Education;
using Language = CvParser.Domain.Entities.CV.V1.Language;
using CvParser.Infrastructure.Interfaces;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel.Design;
using Microsoft.AspNetCore.Http; 
using CvParser.Domain.Enums;
using System.Drawing; 
using Microsoft.Extensions.Logging; 
using System.Linq; 
using System.Text; 
using System; 
using DocumentTypeEnum = CvParser.Domain.Enums.DocumentType;

namespace CvParser.Infrastructure.Services
{
    public class CvService : ICvService
    {
        private readonly string _cvTemplatePath;
        private readonly IDocxService _docxService;
        private readonly IPdfService _pdfService;
        private readonly IImageService _imageService;
        private readonly IAiService _aiService;
        private readonly ILogger<CvService> _logger;

        public CvService(
            IConfiguration configuration,
            IDocxService docxService,
            IPdfService pdfService,
            IImageService imageService,
            IAiService aiService,
            ILogger<CvService> logger)
        {
            _cvTemplatePath = configuration["CvServiceSettings:CvTemplatePath"];

            if (string.IsNullOrEmpty(_cvTemplatePath))
            {
                throw new InvalidOperationException("CV template path is not configured.");
            }

            _docxService = docxService;
            _pdfService = pdfService;
            _imageService = imageService;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<Cv?> ParseCvFromUploadedFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Ingen fil laddades upp eller filen var tom.");
                return null;
            }

            try
            {
                string documentText = string.Empty;
                var documentImages = new List<Image>();
                var documentType = file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? DocumentTypeEnum.PDF : DocumentTypeEnum.Word;

                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    if (documentType == DocumentTypeEnum.PDF)
                    {
                        documentText = _pdfService.GetText(memoryStream);
                        memoryStream.Position = 0;
                        documentImages = _pdfService.GetImages(memoryStream);
                    }
                    else if (documentType == DocumentTypeEnum.Word)
                    {
                        documentText = _docxService.GetText(memoryStream);
                        memoryStream.Position = 0;
                        documentImages = _docxService.GetImages(memoryStream);
                    }
                }

                if (string.IsNullOrWhiteSpace(documentText))
                {
                    _logger.LogInformation("Ingen text kunde extraheras från dokumentet.");
                    return null;
                }

                var resultJson = await _aiService.SendCvAsync(documentText);
                if (string.IsNullOrWhiteSpace(resultJson))
                {
                    _logger.LogInformation("AI-tjänsten returnerade ingen JSON-data.");
                    return null;
                }

                Cv? cv = null;
                try
                {
                    cv = JsonSerializer.Deserialize<Cv>(resultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Kunde inte deserialisera JSON-svaret från AI-tjänsten.");
                    throw new InvalidOperationException("Ogiltigt JSON-format från AI-tjänsten.", ex);
                }

                if (cv is not null && documentImages.Any())
                {
                    var profileImage = documentImages.FirstOrDefault(img => _imageService.HasHumanFace(img));
                    if (profileImage is not null)
                    {
                        cv.PersonalInfo.ImageBase64 = _imageService.ToBase64(profileImage);
                    }
                }

                return cv;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ett fel uppstod vid parsning av den uppladdade filen.");
                throw;
            }
        }

        public async Task<MemoryStream> CreateXameraCV(Cv cv)
        {
            string templateFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _cvTemplatePath);

            if (!File.Exists(templateFile))
            {
                throw new FileNotFoundException($"CV template file not found at: {templateFile}");
            }

            using var templateFileStream = File.OpenRead(templateFile);
            var resultStream = new MemoryStream();n
            templateFileStream.CopyTo(resultStream);
            resultStream.Position = 0;

            using (var doc = WordprocessingDocument.Open(resultStream, true))
            {
                CreateFirstPage(doc, cv);
                NewPage(doc);
                AddAllSkills(doc, cv.Competencies);
                AddProjects(doc, cv.WorkProjects);
                AddWork(doc, cv.Employers);
            }

            resultStream.Position = 0;

            return resultStream;
        }

        private void CreateFirstPage(WordprocessingDocument doc, Cv cv)
        {
            foreach (var textElement in doc.MainDocumentPart.Document.Body.Descendants<Text>())
            {
                if (textElement.ChildElements.Count > 0)
                {
                    var childrens = textElement.ChildElements;
                }

                switch (textElement.Text)
                {
                    case "|Name|":
                        textElement.Text = cv.PersonalInfo.Name;
                        break;
                    case "|Title|":
                        textElement.Text = cv.PersonalInfo.Title;
                        break;
                    case "|Summary|":
                        textElement.Text = cv.Summary;
                        break;
                    case "|MainSkills|":
                        ReplaceMainSkills(doc, cv.Competencies, textElement);
                        break;
                    case "|Educations|":
                        ReplaceEducations(doc, cv.Educations, textElement);
                        break;
                    case "|Languages|":
                        ReplaceLanguages(doc, cv.Languages, textElement);
                        break;
                }
            }
            ReplaceProfilePicture(doc, cv.PersonalInfo.ImageBase64);
        }

        private void NewPage(WordprocessingDocument doc)
        {
            var body = doc.MainDocumentPart.Document.Body;
            var pageBreakParagraph = new Paragraph();
            var run = new Run();
            var breakElement = new Break() { Type = BreakValues.Page };
            run.Append(breakElement);
            pageBreakParagraph.Append(run);
            body.Append(pageBreakParagraph);
        }

        private void ReplaceProfilePicture(WordprocessingDocument doc, string? imageBase64)
        {
            foreach (var imagePart in doc.MainDocumentPart.ImageParts)
            {
                if (!String.IsNullOrEmpty(imageBase64))
                {
                    byte[] imageBytes = Convert.FromBase64String(imageBase64);
                    using (MemoryStream newImageStream = new MemoryStream(imageBytes))
                    {
                        imagePart.FeedData(newImageStream);
                    }
                }
                else
                {
                    List<Drawing> drawingPartsToDelete = new List<Drawing>();
                    List<Drawing> drawingParts = new List<Drawing>(doc.MainDocumentPart.RootElement.Descendants<Drawing>());
                    IEnumerable<Drawing> drawings = drawingParts.Where(d => d.Descendants<PIC.Picture>().Any(p => p.BlipFill.Blip.Embed == doc.MainDocumentPart.GetIdOfPart(imagePart)));

                    foreach (var drawing in drawings)
                    {
                        if (drawing is not null && !drawingPartsToDelete.Contains(drawing))
                        {
                            drawingPartsToDelete.Add(drawing);
                        }
                    }

                    foreach (var drawingPartToDelete in drawingPartsToDelete)
                    {
                        drawingPartToDelete.Remove();
                    }

                    doc.MainDocumentPart.DeleteParts(new List<ImagePart>() { imagePart });
                }
            }
        }

        private void ReplaceMainSkills(WordprocessingDocument doc, List<Competency>? competencies, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;
            parentParagraph.RemoveChild(parentRun);

            try
            {
                if (competencies is not null && competencies.Any())
                {
                    var mainSkills = competencies
                        .Where(c => c.IsMainSkill != c.IsMainSkill)
                        .Select(c => new { CompetencyName = c.Name, KeyWords = c.Keywords! })
                        .ToList();

                    foreach (var skill in mainSkills)
                    {
                        var competencyNameRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" });

                        var competencyNameRun = new Run(competencyNameRunProps, new Text(skill.CompetencyName));
                        parentParagraph.AppendChild(competencyNameRun);

                        foreach (var keyword in skill.KeyWords)
                        {
                            var keywordRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" });

                            var keywordRun = new Run(keywordRunProps, new Text(keyword));
                            parentParagraph.AppendChild(keywordRun);
                        }

                        if (skill != mainSkills.Last())
                        {
                            parentParagraph.AppendChild(new Run(new Break()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid ersättning av huvudfärdigheter i CV.");
            }
        }

        private void ReplaceEducations(WordprocessingDocument doc, List<Education>? educations, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;
            parentParagraph.RemoveChild(parentRun);

            try
            {
                if (educations is not null && educations.Any())
                {
                    foreach (var education in educations)
                    {
                        var yearsRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" },
                            new Bold()
                        );
                        var institutionRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" }
                        );
                        var descriptionRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "18" }
                        );

                        DateTime start;
                        DateTime end;
                        DateTime.TryParse(education.StartDate, null, out start);
                        DateTime.TryParse(education.EndDate, null, out end);

                        var yearStart = (start.Year > 1900) ? start.Year.ToString() : education.StartDate;
                        var yearEnd = (end.Year > 1900) ? end.Year.ToString() : education.EndDate;
                        var yearsText = (yearStart != yearEnd) ? $"{yearStart} - {yearEnd}" : yearStart;

                        var yearsRun = new Run(yearsRunProps, new Text(yearsText));
                        var institutionRun = new Run(institutionRunProps, new Text(education.Institution));
                        var descriptionRun = new Run(descriptionRunProps, new Text(education.StudyType));

                        parentParagraph.AppendChild(yearsRun);
                        parentParagraph.AppendChild(new Run(new Break()));
                        parentParagraph.AppendChild(institutionRun);
                        parentParagraph.AppendChild(new Run(new Break()));
                        parentParagraph.AppendChild(descriptionRun);

                        if (education != educations.Last())
                        {
                            parentParagraph.AppendChild(new Run(new Break()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid ersättning av utbildningar i CV.");
            }
        }

        private void ReplaceLanguages(WordprocessingDocument doc, List<Language>? languages, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;
            parentParagraph.RemoveChild(parentRun);
            bool firstSkill = true;

            foreach (var language in languages)
            {
                if (!firstSkill)
                {
                    parentParagraph.AppendChild(new Run(new Break()));
                }
                firstSkill = false;

                var skillRunProps = new RunProperties(
                    new RunFonts() { Ascii = "Albert Sans" },
                    new FontSize() { Val = "20" }
                );

                var skillRun = new Run(skillRunProps, new Text($"{language.LanguageName}: {language.Fluency}"));
                parentParagraph.AppendChild(skillRun);
            }
        }

        public void ReplacePlaceholderWithImage(WordprocessingDocument doc, string placeholder, string base64Image, Text textElement)
        {
            var mainPart = doc.MainDocumentPart;
            byte[] imageBytes = Convert.FromBase64String(base64Image);
            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var stream = new MemoryStream(imageBytes))
            {
                imagePart.FeedData(stream);
            }

            string imagePartId = mainPart.GetIdOfPart(imagePart);
            var run = textElement.Parent as Run;
            var paragraph = run.Parent as Paragraph;
            run.Remove();

            var drawing = CreateImage(mainPart, imagePartId, "ProfileImage", 500, 500);
            var imageRun = new Run(drawing);
            paragraph.Append(imageRun);
        }

        private void AddHeader(WordprocessingDocument doc, string name)
        {
            var headerRunProps = new RunProperties(
                new RunFonts() { Ascii = "Albert Sans" },
                new FontSize() { Val = "32" },
                new Bold());
            var headerRun = new Run(headerRunProps, new Text(name));
            var headerParagraph = new Paragraph(headerRun);
            var spacerParagraph = new Paragraph(new Run(new Text("")));
            var body = doc.MainDocumentPart.Document.Body;
            body.Append(headerParagraph);
            body.Append(spacerParagraph);
        }

        private void AddAllSkills(WordprocessingDocument doc, List<Competency>? competencies)
        {
            if (competencies is not null && competencies.Any())
            {
                AddHeader(doc, "Skills");
                var body = doc.MainDocumentPart.Document.Body;

                foreach (var competency in competencies)
                {
                    Table table = new Table();
                    TableProperties tblProps = new TableProperties(
                        new TableBorders(
                            new TopBorder { Val = BorderValues.None },
                            new BottomBorder { Val = BorderValues.None },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None },
                            new InsideHorizontalBorder { Val = BorderValues.None },
                            new InsideVerticalBorder { Val = BorderValues.None }
                        ),
                        new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
                    );

                    var grid = new TableGrid(
                        new GridColumn() { Width = "2500" },
                        new GridColumn() { Width = "2500" }
                    );

                    table.AppendChild(tblProps);
                    table.AppendChild(grid);

                    TableRow trHeader = new TableRow();
                    Run runHeader = new Run();
                    RunProperties runPropsHeader = new RunProperties(
                        new RunFonts() { Ascii = "Albert Sans" },
                        new FontSize() { Val = "24" },
                        new Bold()
                    );
                    Paragraph paraHeader = new Paragraph(runHeader);
                    TableCell tcHeader = new TableCell(paraHeader);

                    runHeader.Append(runPropsHeader);
                    runHeader.Append(new Text(competency.SkillCategory ?? string.Empty));
                    tcHeader.Append(new TableCellProperties(
                        new TableCellBorders(
                            new TopBorder { Val = BorderValues.None },
                            new BottomBorder { Val = BorderValues.None },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None }
                        )
                    ));
                    trHeader.Append(tcHeader);
                    trHeader.Append(new TableCell(new Paragraph(new Run(new Text(string.Empty)))));
                    table.Append(trHeader);

                    for (int i = 0; i < competency.Keywords.Count; i += 2)
                    {
                        bool extraBreakLine = (competency.Keywords.Count > 1 && i >= competency.Keywords.Count - 2) ? true : false;
                        TableRow tr = new TableRow();
                        var cell1 = CreateCell(competency.Keywords[i], extraBreakLine);
                        tr.Append(cell1);

                        if (i + 1 < competency.Keywords.Count)
                        {
                            var cell2 = CreateCell(competency.Keywords[i + 1], extraBreakLine);
                            tr.Append(cell2);
                        }

                        table.Append(tr);
                    }
                    body.Append(table);
                }
            }
        }

        private TableCell CreateCell(string? text, bool extraBreakline = false, string fontSize = "20")
        {
            var runProps = new RunProperties(
                new RunFonts() { Ascii = "Albert Sans" },
                new FontSize() { Val = fontSize }
            );
            var run = new Run(runProps);
            run.Append(new Text(text ?? string.Empty));

            if (extraBreakline)
            {
                run.Append(new Break());
            }

            var para = new Paragraph(run);

            var cellProps = new TableCellProperties(
                new TableCellBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None }
                )
            );

            var cell = new TableCell(para);
            cell.Append(cellProps);

            return cell;
        }

        private void AddProjects(WordprocessingDocument doc, List<WorkProject>? projects)
        {
            if (projects is not null && projects.Any())
            {
                NewPage(doc);
                AddHeader(doc, "Projects");
            }
        }

        private void AddWork(WordprocessingDocument doc, List<Employer>? works)
        {
            if (works is not null && works.Any())
            {
                NewPage(doc);
                AddHeader(doc, "Work");
            }
        }

        private Drawing CreateImage(MainDocumentPart mainPart, string relationshipId, string name, long width, long height)
        {
            return new Drawing(
                new WP.Inline(
                    new WP.Extent() { Cx = width, Cy = height },
                    new WP.EffectExtent()
                    {
                        LeftEdge = 0L,
                        TopEdge = 0L,
                        RightEdge = 0L,
                        BottomEdge = 0L
                    },
                    new WP.DocProperties()
                    {
                        Id = (UInt32Value)1U,
                        Name = name
                    },
                    new DW.Graphic(
                        new DW.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties()
                                    {
                                        Id = (UInt32Value)0U,
                                        Name = name
                                    },
                                    new PIC.NonVisualPictureDrawingProperties()
                                ),
                                new PIC.BlipFill(
                                    new DW.Blip()
                                    {
                                        Embed = relationshipId,
                                        CompressionState = DW.BlipCompressionValues.Print
                                    },
                                    new DW.Stretch(
                                        new DW.FillRectangle()
                                    )
                                ),
                                new PIC.ShapeProperties(
                                    new DW.Transform2D(
                                        new DW.Offset() { X = 0L, Y = 0L },
                                        new DW.Extents() { Cx = width, Cy = height }
                                    ),
                                    new DW.PresetGeometry(
                                        new DW.AdjustValueList()
                                    )
                                    { Preset = DW.ShapeTypeValues.Rectangle }
                                )
                            )
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                    )
                )
                {
                    DistanceFromTop = (UInt32Value)0U,
                    DistanceFromBottom = (UInt32Value)0U,
                    DistanceFromLeft = (UInt32Value)0U,
                    DistanceFromRight = (UInt32Value)0U
                }
            );
        }
    }
}
*/

/*
using Microsoft.Extensions.Configuration;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using DW = DocumentFormat.OpenXml.Drawing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;
using Break = DocumentFormat.OpenXml.Wordprocessing.Break;
using Drawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using FontSize = DocumentFormat.OpenXml.Wordprocessing.FontSize;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using CvParser.Domain.Entities.CV.V1;
using Cv = CvParser.Domain.Entities.CV.V1.Cv;
using Education = CvParser.Domain.Entities.CV.V1.Education;
using Language = CvParser.Domain.Entities.CV.V1.Language;
using CvParser.Infrastructure.Interfaces;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel.Design;

namespace CvParser.Infrastructure.Services
{
    public class CvService : ICvService
    {  
        private readonly string _cvTemplatePath;
        private readonly IDocxService _docxService;
        private readonly IAiService _aiService;

        // Beroendeinjektion av IConfiguration och de nya tjänsterna
        public CvService(IConfiguration configuration, IDocxService docxService, IAiService aiService)
        {
            _cvTemplatePath = configuration["CvServiceSettings:CvTemplatePath"];

            if (string.IsNullOrEmpty(_cvTemplatePath))
            {
                throw new InvalidOperationException("CV template path is not configured.");
            }
            
            _docxService = docxService;
            _aiService = aiService;
        }      


        // Den använder IDocxService för att läsa filen och IAiService för att tolka den.
        public async Task<Cv> ParseCvFromUploadedFileAsync(IFormFile file)
        {
            var documentText = string.Empty;
            // ... extrahera text och bilder här ...

            var resultJson = await _aiService.SendCvAsync(documentText);
            // ... deserialisera och hantera bilder ...
            return cv;
        }

        // Här är din ursprungliga metod för att skapa en Word-fil från en Cv-modell.
        public async Task<MemoryStream> CreateXameraCV(Cv cv)
        {
            // Få den fullständiga sökvägen till mallen
            string templateFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _cvTemplatePath);

            // Verifiera att filen finns innan du försöker öppna den
            if (!File.Exists(templateFile))
            {
                throw new FileNotFoundException($"CV template file not found at: {templateFile}");
            }
            
            // Open template file stream (read-only)
            using var templateFileStream = File.OpenRead(templateFile);

            var memoryStream = new MemoryStream();
            //string folderPath = @"C:\_DEV\CVs";
            //string fileTemplate = @$"{folderPath}\CV-FirstPage-Template.docx";

            // Open template file stream (read-only)
            //using var templateFileStream = File.OpenRead(fileTemplate);

            // Copy to memory stream (so we can edit it in memory if needed)
            var resultStream = new MemoryStream();
            templateFileStream.CopyTo(resultStream);
            resultStream.Position = 0;

            // Open the Word document from memory stream
            using (var doc = WordprocessingDocument.Open(resultStream, true))
            {
                CreateFirstPage(doc, cv);
                NewPage(doc);
                AddAllSkills(doc, cv.Competencies);
                AddProjects(doc, cv.WorkProjects);                
                AddWork(doc, cv.Employers);
            }

            resultStream.Position = 0;

            return resultStream;
        }

        private void CreateFirstPage(WordprocessingDocument doc, Cv cv)
        {
            foreach (var textElement in doc.MainDocumentPart.Document.Body.Descendants<Text>())
            {
                if (textElement.ChildElements.Count > 0)
                {
                    var childrens = textElement.ChildElements;
                }

                switch (textElement.Text)
                {
                    case "|Name|":
                        textElement.Text = cv.PersonalInfo.Name;
                        break;
                    case "|Title|":
                        textElement.Text = cv.PersonalInfo.Title;
                        break;
                    case "|Summary|":
                        textElement.Text = cv.Summary;
                        break;
                    case "|MainSkills|":
                        ReplaceMainSkills(doc, cv.Competencies, textElement);
                        break;
                    case "|Educations|":
                        ReplaceEducations(doc, cv.Educations, textElement);
                        break;
                    case "|Languages|":
                        ReplaceLanguages(doc, cv.Languages, textElement);
                        break;
                }
            }

            ReplaceProfilePicture(doc, cv.PersonalInfo.ImageBase64);
        }
        private void NewPage(WordprocessingDocument doc)
        {                        
            var body = doc.MainDocumentPart.Document.Body;
            var pageBreakParagraph = new Paragraph();
            var run = new Run();
            var breakElement = new Break() { Type = BreakValues.Page };
            run.Append(breakElement);
            pageBreakParagraph.Append(run);
            body.Append(pageBreakParagraph);
        }

        #region First Page
        private void ReplaceProfilePicture(WordprocessingDocument doc, string? imageBase64)
        {
            foreach (var imagePart in doc.MainDocumentPart.ImageParts)
            {
                // Replace profile image
                if (!String.IsNullOrEmpty(imageBase64))
                {
                    byte[] imageBytes = Convert.FromBase64String(imageBase64);

                    using (MemoryStream newImageStream = new MemoryStream(imageBytes))
                    {
                        imagePart.FeedData(newImageStream);
                    }
                }
                else // Delete profileimage placeholder
                {
                    List<Drawing> drawingPartsToDelete = new List<Drawing>();
                    List<Drawing> drawingParts = new List<Drawing>(doc.MainDocumentPart.RootElement.Descendants<Drawing>());
                    IEnumerable<Drawing> drawings = drawingParts.Where(d => d.Descendants<PIC.Picture>().Any(p => p.BlipFill.Blip.Embed == doc.MainDocumentPart.GetIdOfPart(imagePart)));

                    foreach (var drawing in drawings)
                    {
                        if (drawing is not null && !drawingPartsToDelete.Contains(drawing))
                        {
                            drawingPartsToDelete.Add(drawing);
                        }
                    }

                    // Must have this
                    foreach (var drawingPartToDelete in drawingPartsToDelete)
                    {
                        drawingPartToDelete.Remove();
                    }

                    doc.MainDocumentPart.DeleteParts(new List<ImagePart>() { imagePart });
                }
            }
        }

        private void ReplaceMainSkills(WordprocessingDocument doc, List<Competency>? competencies, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;

            // Remove the placeholder run
            parentParagraph.RemoveChild(parentRun);

            try
            {
                if (competencies is not null && competencies.Any())
                {
                    var mainSkills = competencies
                        .Where(c => c.IsMainSkill != c.IsMainSkill)
                        .Select(c => new { CompetencyName = c.Name, KeyWords = c.Keywords! })
                        .ToList();

                    foreach (var skill in mainSkills)
                    {
                        var competencyNameRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" });

                            var  competencyNameRun = new Run(competencyNameRunProps, new Text(skill.CompetencyName));
                            parentParagraph.AppendChild(competencyNameRun);

                        foreach (var keyword in skill.KeyWords)
                        {
                            var keywordRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" }
                            );

                            var keywordRun = new Run(keywordRunProps, new Text(keyword));
                            parentParagraph.AppendChild(keywordRun);
                        }
                                                            
                        if (skill != mainSkills.Last())
                        {
                            parentParagraph.AppendChild(new Run(new Break()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        
        private void ReplaceEducations(WordprocessingDocument doc, List<Education>? educations, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;

            // Remove the placeholder
            parentParagraph.RemoveChild(parentRun);

            try
            {
                if (educations is not null && educations.Any())
                {
                    foreach (var education in educations)
                    {

                        var yearsRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" },
                            new Bold()
                        );
                        var institutionRunProps = new RunProperties(
                            new RunFonts() { Ascii = "Albert Sans" },
                            new FontSize() { Val = "20" }
                        );
                        var descriptionRunProps = new RunProperties(
                           new RunFonts() { Ascii = "Albert Sans" },
                           new FontSize() { Val = "18" }
                       );

                        DateTime start;
                        DateTime end;
                        DateTime.TryParse(education.StartDate, null, out start);
                        DateTime.TryParse(education.EndDate, null, out end);

                        var yearStart = (start.Year > 1900) ? start.Year.ToString() : education.StartDate;
                        var yearEnd = (end.Year > 1900) ? end.Year.ToString() : education.EndDate;
                        var yearsText = (yearStart != yearEnd) ? $"{yearStart} - {yearEnd}" : yearStart;

                        var yearsRun = new Run(yearsRunProps, new Text(yearsText));
                        var institutionRun = new Run(institutionRunProps, new Text(education.Institution));
                        var descriptionRun = new Run(descriptionRunProps, new Text(education.StudyType));

                        parentParagraph.AppendChild(yearsRun);
                        parentParagraph.AppendChild(new Run(new Break()));
                        parentParagraph.AppendChild(institutionRun);
                        parentParagraph.AppendChild(new Run(new Break()));
                        parentParagraph.AppendChild(descriptionRun);

                        if (education != educations.Last())
                        {
                            parentParagraph.AppendChild(new Run(new Break()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void ReplaceLanguages(WordprocessingDocument doc, List<Language>? languages, Text textElement)
        {
            var parentRun = (Run)textElement.Parent;
            var parentParagraph = (Paragraph)parentRun.Parent;

            // Remove the placeholder run
            parentParagraph.RemoveChild(parentRun);
            bool firstSkill = true;

            foreach (var language in languages)
            {
                // Add a line break before each new skill name except the first
                if (!firstSkill)
                {
                    parentParagraph.AppendChild(new Run(new Break()));
                }
                firstSkill = false;

                // Bold Skill Name, Albert Sans 11pt (22 half-points)
                var skillRunProps = new RunProperties(
                    new RunFonts() { Ascii = "Albert Sans" },
                    new FontSize() { Val = "20" }
                );

                var skillRun = new Run(skillRunProps, new Text($"{language.LanguageName}: {language.Fluency}"));
                parentParagraph.AppendChild(skillRun);
            }
        }

        public void ReplacePlaceholderWithImage(WordprocessingDocument doc, string placeholder, string base64Image, Text textElement)
        {
            var mainPart = doc.MainDocumentPart;

            // Decode the base64 image into a byte array
            byte[] imageBytes = Convert.FromBase64String(base64Image);

            // Add image part to the document
            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var stream = new MemoryStream(imageBytes))
            {
                imagePart.FeedData(stream);
            }

            // Get relationship ID
            string imagePartId = mainPart.GetIdOfPart(imagePart);                        
            var run = textElement.Parent as Run;
            var paragraph = run.Parent as Paragraph;

            // Remove the placeholder run
            run.Remove();

            // Create Drawing element (image)
            var drawing = CreateImage(mainPart, imagePartId, "ProfileImage", 500, 500);

            // Add image to a new run
            var imageRun = new Run(drawing);

            // Insert the image run into the paragraph
            paragraph.Append(imageRun);                        
        }
        #endregion

        #region Other Pages

        private void AddHeader(WordprocessingDocument doc, string name)
        {            
            var headerRunProps = new RunProperties(
                new RunFonts() { Ascii = "Albert Sans" },
                new FontSize() { Val = "32" },
                new Bold());
            var headerRun = new Run(headerRunProps, new Text(name));
            var headerParagraph = new Paragraph(headerRun);
            var spacerParagraph = new Paragraph(new Run(new Text("")));

            var body = doc.MainDocumentPart.Document.Body;
            body.Append(headerParagraph);
            body.Append(spacerParagraph);
        }

        private void AddAllSkills(WordprocessingDocument doc, List<Competency>? competencies)
        {
            if (competencies is not null && competencies.Any())
            {
                AddHeader(doc, "Skills");
                var body = doc.MainDocumentPart.Document.Body;

                foreach (var competency in competencies)
                {
                    Table table = new Table();
                    TableProperties tblProps = new TableProperties(
                        new TableBorders(
                            new TopBorder { Val = BorderValues.None },
                            new BottomBorder { Val = BorderValues.None },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None },
                            new InsideHorizontalBorder { Val = BorderValues.None },
                            new InsideVerticalBorder { Val = BorderValues.None }
                        ),
                        new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
                    );
                    
                    var grid = new TableGrid(
                        new GridColumn() { Width = "2500" },
                        new GridColumn() { Width = "2500" }
                    );

                    table.AppendChild(tblProps);
                    table.AppendChild(grid);

                    TableRow trHeader = new TableRow();
                    Run runHeader = new Run();
                    RunProperties runPropsHeader = new RunProperties(
                        new RunFonts() { Ascii = "Albert Sans" },
                        new FontSize() { Val = "24" },
                        new Bold()
                    );
                    Paragraph paraHeader = new Paragraph(runHeader);
                    TableCell tcHeader = new TableCell(paraHeader);

                    runHeader.Append(runPropsHeader);                    
                    runHeader.Append(new Text(competency.SkillCategory ?? string.Empty));
                    tcHeader.Append(new TableCellProperties(
                        new TableCellBorders(
                            new TopBorder { Val = BorderValues.None },
                            new BottomBorder { Val = BorderValues.None },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None }
                        )
                    ));
                    trHeader.Append(tcHeader);                    
                    trHeader.Append(new TableCell(new Paragraph(new Run(new Text(string.Empty)))));
                    table.Append(trHeader);

                    for (int i = 0; i < competency.Keywords.Count; i += 2)
                    {
                        bool extraBreakLine = (competency.Keywords.Count > 1 && i >= competency.Keywords.Count - 2) ? true : false;
                        TableRow tr = new TableRow();
                        var cell1 = CreateCell(competency.Keywords[i], extraBreakLine);
                        tr.Append(cell1);

                        if (i + 1 < competency.Keywords.Count)
                        {
                            var cell2 = CreateCell(competency.Keywords[i + 1], extraBreakLine);
                            tr.Append(cell2);
                        }                        

                        table.Append(tr);
                    }

                    body.Append(table);
                }
            }
        }

        private TableCell CreateCell(string? text, bool extraBreakline = false, string fontSize = "20")
        {
            var runProps = new RunProperties(
                new RunFonts() { Ascii = "Albert Sans" },
                new FontSize() { Val = fontSize }
            );

            var run = new Run(runProps);
            run.Append(new Text(text ?? string.Empty));

            if (extraBreakline)
            {
                run.Append(new Break());
            }

            var para = new Paragraph(run);

            var cellProps = new TableCellProperties(
                new TableCellBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None }
                )
            );

            var cell = new TableCell(para);
            cell.Append(cellProps);

            return cell;
        }

        private void AddProjects(WordprocessingDocument doc, List<WorkProject>? projects)
        {
            if (projects is not null && projects.Any())
            {
                NewPage(doc);
                AddHeader(doc, "Projects");
            }
        }

        private void AddWork(WordprocessingDocument doc, List<Employer>? works)
        {
            if (works is not null && works.Any())
            {
                NewPage(doc);
                AddHeader(doc, "Work");


            }
        }
        #endregion

        private Drawing CreateImage(MainDocumentPart mainPart, string relationshipId, string name, long width, long height)
        {
            return new Drawing(
                new WP.Inline(
                    new WP.Extent() { Cx = width, Cy = height },
                    new WP.EffectExtent()
                    {
                        LeftEdge = 0L,
                        TopEdge = 0L,
                        RightEdge = 0L,
                        BottomEdge = 0L
                    },
                    new WP.DocProperties()
                    {
                        Id = (UInt32Value)1U,
                        Name = name
                    },
                    new DW.Graphic(
                        new DW.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties()
                                    {
                                        Id = (UInt32Value)0U,
                                        Name = name
                                    },
                                    new PIC.NonVisualPictureDrawingProperties()
                                ),
                                new PIC.BlipFill(
                                    new DW.Blip()
                                    {
                                        Embed = relationshipId,
                                        CompressionState = DW.BlipCompressionValues.Print
                                    },
                                    new DW.Stretch(
                                        new DW.FillRectangle()
                                    )
                                ),
                                new PIC.ShapeProperties(
                                    new DW.Transform2D(
                                        new DW.Offset() { X = 0L, Y = 0L },
                                        new DW.Extents() { Cx = width, Cy = height }
                                    ),
                                    new DW.PresetGeometry(
                                        new DW.AdjustValueList()
                                    )
                                    { Preset = DW.ShapeTypeValues.Rectangle }
                                )
                            )
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                    )
                )
                {
                    DistanceFromTop = (UInt32Value)0U,
                    DistanceFromBottom = (UInt32Value)0U,
                    DistanceFromLeft = (UInt32Value)0U,
                    DistanceFromRight = (UInt32Value)0U
                }
            );
        }
    }
}
*/