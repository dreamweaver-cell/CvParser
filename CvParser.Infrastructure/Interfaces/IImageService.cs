using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace CvParser.Infrastructure.Interfaces
{
    public interface IImageService
    {
        string ExtractTextFromDocument(Stream stream, string extension);
        Task<IReadOnlyList<Image>> ExtractImagesFromPdfAsync(Stream pdfStream);
        Task<Image[]> ExtractImagesFromDocxAsync(Stream docxStream);
        Task<bool> HasHumanFaceInDocumentAsync(Stream fileStream, string extension);
        bool HasHumanFace(Image img);
        Task<string> ToBase64Async(Image image);
        Image FromBase64(string base64);

        // word-embeddning
        bool TryPrepareInlinePng(
            string base64,
            int maxWidthPx,
            int maxHeightPx,
            out byte[] pngBytes,
            out int widthPx,
            out int heightPx,
            out int dpiX,
            out int dpiY);
    }
}
