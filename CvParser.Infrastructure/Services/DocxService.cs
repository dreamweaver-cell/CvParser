using CvParser.Infrastructure.Interfaces;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xceed.Words.NET;

namespace CvParser.Infrastructure.Services
{
    public class DocxService : IDocxService
    {
        /// <summary>
        /// Asynkront extraherar all text från en DOCX-filström.
        /// </summary>
        /// <param name="fileStream">Filströmmen för DOCX-filen.</param>
        /// <returns>En sträng som innehåller den extraherade texten.</returns>
        public Task<string> ExtractTextFromDocxAsync(Stream fileStream)
        {
            // Kontrollerar om filströmmen är null.
            if (fileStream == null || fileStream.Length == 0)
            {
                return Task.FromResult(string.Empty);
            }

            try
            {
                // Öppnar filströmmen som ett WordprocessingDocument för att läsa den.
                // Det andra argumentet 'false' anger att filen är skrivskyddad.
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(fileStream, false))
                {
                    // Använder en StringBuilder för att effektivt bygga upp den extraherade texten.
                    StringBuilder sb = new StringBuilder();

                    // Hämtar huvuddelen av dokumentet.
                    Body? body = wordDoc.MainDocumentPart?.Document.Body;

                    if (body != null)
                    {
                        // Itererar igenom varje paragraf (stycke) i dokumentet.
                        foreach (var paragraph in body.Descendants<Paragraph>())
                        {
                            // Hämtar texten från varje stycke och lägger till den i StringBuilder.
                            sb.AppendLine(paragraph.InnerText);
                        }
                    }

                    // Returnerar den extraherade texten.
                    return Task.FromResult(sb.ToString());
                }
            }
            catch
            {
                // Returnerar en tom sträng om ett fel uppstår under läsningen.
                return Task.FromResult(string.Empty);
            }
        }

        public string GetText(Stream fileStream)
        {
            using (DocX document = DocX.Load(fileStream))
            {
                return document.Text;
            }
        }

        public List<Image> GetImages(Stream fileStream)
        {
            try
            {
                var images = new List<Image>();
                var seenNames = new HashSet<string>();

                using (DocX document = DocX.Load(fileStream))
                {
                    foreach (var paragraph in document.Paragraphs)
                    {
                        foreach (var picture in paragraph.Pictures)
                        {
                            if (!seenNames.Contains(picture.Name))
                            {
                                // Copy stream bytes
                                byte[] imageBytes;
                                using (var ms = new MemoryStream())
                                {
                                    picture.Stream.CopyTo(ms);
                                    imageBytes = ms.ToArray();
                                }

                                using (var imgStream = new MemoryStream(imageBytes))
                                {
                                    Image img = Image.FromStream(imgStream);
                                    images.Add(new Bitmap(img));
                                }

                                // Mark as seen
                                seenNames.Add(picture.Name);
                            }
                        }
                    }
                }

                return images;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        [Obsolete]
        public string GetTextOpenXML(Stream fileStream)
        {
            StringBuilder textBuilder = new StringBuilder();

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(fileStream, false))
            {
                var body = wordDoc.MainDocumentPart?.Document.Body;

                foreach (var element in body.Elements())
                {
                    ExtractTextWithPageBreaks(element, textBuilder);
                }
            }

            return textBuilder.ToString();
        }

        [Obsolete]
        public List<Image> GetImagesOpenXML(Stream fileStream)
        {
            List<Image> images = new List<Image>();

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(fileStream, false))
            {
                // Access the main document part
                var mainPart = wordDoc.MainDocumentPart;

                // Check if there are any embedded images
                if (mainPart?.ImageParts != null)
                {
                    foreach (var imagePart in mainPart.ImageParts)
                    {
                        using (var imageStream = imagePart.GetStream())
                        {
                            // Create an image object from the image stream and add it to the list
                            try
                            {
                                Image img = Image.FromStream(imageStream);
                                images.Add(img);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error loading image: {ex.Message}");
                            }
                        }
                    }
                }
            }

            return images;
        }

        [Obsolete]
        private void ExtractTextWithPageBreaks(OpenXmlElement element, StringBuilder builder)
        {
            try
            {

                foreach (var child in element.Elements())
                {
                    if (child is Paragraph paragraph)
                    {
                        foreach (var run in paragraph.Elements<Run>())
                        {
                            foreach (var runChild in run.Elements())
                            {
                                if (runChild is Text text)
                                {
                                    builder.Append(text.Text);
                                }
                                else if (runChild is Break br)
                                {
                                    if (br.Type != null && br.Type == BreakValues.Page)
                                    {
                                        builder.AppendLine();
                                        builder.AppendLine();
                                    }
                                }
                            }
                        }
                        builder.AppendLine();
                    }
                    else
                    {
                        ExtractTextWithPageBreaks(child, builder);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
}
