/*
namespace CvParser.Infrastructure.Services;

public class PdfService : IPdfService
{
    public string GetText(Stream pdfStream)
    {
        StringBuilder text = new StringBuilder();

        if (pdfStream.CanSeek)
        {
            pdfStream.Seek(0, SeekOrigin.Begin);
        }

        using (PdfReader reader = new PdfReader(pdfStream))
        using (PdfDocument pdfDoc = new PdfDocument(reader))
        {
            for (int page = 1; page <= pdfDoc.GetNumberOfPages(); page++)
            {
                var strategy = new LocationTextExtractionStrategy();
                string pageContent = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(page), strategy);
                text.AppendLine(pageContent);
            }
        }

        return text.ToString();
    }

    public List<Image> GetImages(Stream pdfStream)
    {
        try
        {
            var imageList = new List<Image>();

            using (var reader = new PdfReader(pdfStream))
            using (var pdfDoc = new PdfDocument(reader))
            {
                for (int pageNumber = 1; pageNumber <= pdfDoc.GetNumberOfPages(); pageNumber++)
                {
                    var page = pdfDoc.GetPage(pageNumber);
                    var resources = page.GetResources();
                    var xObjects = resources.GetResource(PdfName.XObject);

                    if (xObjects == null) continue;

                    foreach (var xObjectName in xObjects.KeySet())
                    {
                        var xObject = xObjects.GetAsStream(xObjectName);
                        if (xObject == null) continue;

                        var subtype = xObject.GetAsName(PdfName.Subtype);
                        if (subtype != null && subtype.Equals(PdfName.Image))
                        {
                            var imageObject = new PdfImageXObject(xObject);
                            var imageBytes = imageObject.GetImageBytes(true);

                            if (imageBytes != null)
                            {
                                using (var ms = new MemoryStream(imageBytes))
                                {
                                    var image = Image.FromStream(ms);
                                    imageList.Add(image);
                                }
                            }
                        }
                    }
                }
            }

            return imageList;
        }
        catch (Exception ex)
        {
            return [];
        }
    }
}
*/