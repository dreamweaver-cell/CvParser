using Microsoft.Extensions.Logging;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;


namespace CvParser.Infrastructure.Services
{
    public class ImageService : IImageService
    {
        private readonly ILogger<ImageService> _logger;
        private readonly CascadeClassifier _faceCascade;

        public ImageService(ILogger<ImageService> logger)
        {
            _logger = logger;
            var cascadePath = Path.Combine(AppContext.BaseDirectory, "Resources", "Haarcascades", "haarcascade_frontalface_default.xml");
            if (!File.Exists(cascadePath))
                throw new FileNotFoundException($"Cascade-filen saknas: {cascadePath}");
            _faceCascade = new CascadeClassifier(cascadePath);
        }


        // Face detection helpers
        public bool HasHumanFace(Image img)
        {
            if (img == null) return false;
            try
            {
                using var bmp = ConvertToRgbBitmap(img);
                using var mat = BitmapToMat(bmp);
                if (mat.IsEmpty) return false;
                // Convert to grayscale as face detection typically works better on grayscale images
                using var gray = new Mat();
                CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);
                return _faceCascade.DetectMultiScale(gray, 1.1, 4).Any();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fel vid ansiktsdetektion.");
                return false;
            }
        }

        // Convert image to 24bpp RGB bitmap if needed
        private static Bitmap ConvertToRgbBitmap(Image image)
        {
            if (image.PixelFormat != System.Drawing.Imaging.PixelFormat.Format24bppRgb)
            {
                var bmp = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(bmp);
                g.DrawImage(image, 0, 0, image.Width, image.Height);
                return bmp;
            }
            return new Bitmap(image);
        }

        // Convert Bitmap to Emgu CV Mat
        private static Mat BitmapToMat(Bitmap bitmap)
        {
            var clone = new Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(clone)) g.DrawImage(bitmap, 0, 0, clone.Width, clone.Height);
            var rect = new Rectangle(0, 0, clone.Width, clone.Height);
            var data = clone.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, clone.PixelFormat);
            try
            {
                return new Mat(clone.Height, clone.Width, DepthType.Cv8U, 3, data.Scan0, data.Stride);
            }
            finally { clone.UnlockBits(data); }
        }


        // PdfPig: image extraction
        public async Task<IReadOnlyList<Image>> ExtractImagesFromPdfAsync(Stream pdfStream)
        {
            if (pdfStream == null) throw new ArgumentNullException(nameof(pdfStream));
            if (!pdfStream.CanSeek)
            {
                var ms = new MemoryStream();
                await pdfStream.CopyToAsync(ms);
                ms.Position = 0;
                pdfStream = ms;
            }

            var images = new List<Image>();
            using (var pdf = PdfDocument.Open(pdfStream))
            {
                foreach (var page in pdf.GetPages())
                {
                    foreach (var img in page.GetImages())
                    {
                        try
                        {
                            using var ms = new MemoryStream(img.RawBytes.ToArray());
                            images.Add(System.Drawing.Image.FromStream(ms));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Kunde inte läsa en inbäddad PDF-bild.");
                        }
                    }
                }
            }
            return images;
        }

        // OpenXML: image extraction
        public async Task<Image[]> ExtractImagesFromDocxAsync(Stream docxStream)
        {
            using var ms = new MemoryStream();
            await docxStream.CopyToAsync(ms);
            ms.Position = 0;

            using var doc = WordprocessingDocument.Open(ms, false);
            var imageParts = doc.MainDocumentPart?.ImageParts;
            if (imageParts == null) return Array.Empty<Image>();

            var images = new List<Image>();
            foreach (var part in imageParts)
            {
                try
                {
                    using var stream = part.GetStream();
                    var img = await Task.Run(() => Image.FromStream(stream));
                    if (img != null) images.Add(img);
                }
                catch { }
            }
            return images.ToArray();
        }

        // PdfPig: text extraction
        private static string ExtractTextFromPdf(Stream pdfStream)
        {
            try
            {
                if (!pdfStream.CanSeek)
                {
                    using var temp = new MemoryStream();
                    pdfStream.CopyTo(temp);
                    temp.Position = 0;
                    pdfStream = temp;
                }

                using var pdf = PdfDocument.Open(pdfStream);
                var sb = new StringWriter();
                foreach (var page in pdf.GetPages())
                    sb.WriteLine(page.Text);

                return sb.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // OpenXML: text extraction
        private static string ExtractTextFromDocx(Stream docxStream)
        {
            try
            {
                using var ms = new MemoryStream();
                docxStream.CopyTo(ms);
                ms.Position = 0;

                using var doc = WordprocessingDocument.Open(ms, false);
                var body = doc.MainDocumentPart?.Document.Body;
                if (body == null) return string.Empty;
                return string.Join(Environment.NewLine,
                    body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
                        .Select(t => t.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t)));
            }
            catch { return string.Empty; }
        }

        public string ExtractTextFromDocument(Stream stream, string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => ExtractTextFromPdf(stream),
                ".docx" => ExtractTextFromDocx(stream),
                _ => string.Empty
            };
        }


        // Combined helper logic
        public async Task<bool> HasHumanFaceInDocumentAsync(Stream fileStream, string extension)
        {
            Image[] images = extension.ToLower() switch
            {
                ".pdf"  => (await ExtractImagesFromPdfAsync(fileStream)).ToArray(),
                ".docx" => await ExtractImagesFromDocxAsync(fileStream),
                _       => Array.Empty<Image>()
            };
            try { return images.Any(HasHumanFace); }
            finally { foreach (var img in images) img?.Dispose(); }
        }

        public async Task<string> ToBase64Async(Image image)
        {
            if (image == null) return null;
            using var bmp = new Bitmap(image);
            using var ms = new MemoryStream();
            await Task.Run(() => bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png));
            return Convert.ToBase64String(ms.ToArray());
        }

        public Image FromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            return Image.FromStream(ms);
        }

        // Prepare inline PNG (resize, auto-rotate, convert)
        public bool TryPrepareInlinePng(string base64, int maxWidthPx, int maxHeightPx,
            out byte[] pngBytes, out int widthPx, out int heightPx, out int dpiX, out int dpiY)
        {
            pngBytes = Array.Empty<byte>();
            widthPx = heightPx = 0;
            dpiX = dpiY = 96;

            try
            {
                if (string.IsNullOrWhiteSpace(base64)) return false;
                var bytes = Convert.FromBase64String(base64);
                using var srcStream = new MemoryStream(bytes);
                using var src = Image.FromStream(srcStream);

                TryAutoRotate(src);

                dpiX = (int)Math.Round(src.HorizontalResolution);
                dpiY = (int)Math.Round(src.VerticalResolution);
                if (dpiX <= 0) dpiX = 96;
                if (dpiY <= 0) dpiY = 96;

                double rx = maxWidthPx > 0 ? (double)maxWidthPx / src.Width : 1.0;
                double ry = maxHeightPx > 0 ? (double)maxHeightPx / src.Height : 1.0;
                double ratio = Math.Min(1.0, Math.Min(rx, ry));

                int dstW = Math.Max(1, (int)Math.Round(src.Width * ratio));
                int dstH = Math.Max(1, (int)Math.Round(src.Height * ratio));

                using var bmp = new Bitmap(dstW, dstH);
                bmp.SetResolution(src.HorizontalResolution, src.VerticalResolution);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(src, 0, 0, dstW, dstH);
                }

                using var outMs = new MemoryStream();
                bmp.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
                pngBytes = outMs.ToArray();
                widthPx = dstW;
                heightPx = dstH;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TryPrepareInlinePng misslyckades.");
                return false;
            }
        }

        private static void TryAutoRotate(Image img)
        {
            try
            {
                const int OrientationId = 0x0112;
                if (!img.PropertyIdList.Contains(OrientationId)) return;
                var prop = img.GetPropertyItem(OrientationId);
                int orient = prop.Value[0];
                var flip = orient switch
                {
                    2 => RotateFlipType.RotateNoneFlipX,
                    3 => RotateFlipType.Rotate180FlipNone,
                    4 => RotateFlipType.Rotate180FlipX,
                    5 => RotateFlipType.Rotate90FlipX,
                    6 => RotateFlipType.Rotate90FlipNone,
                    7 => RotateFlipType.Rotate270FlipX,
                    8 => RotateFlipType.Rotate270FlipNone,
                    _ => RotateFlipType.RotateNoneFlipNone
                };
                if (flip != RotateFlipType.RotateNoneFlipNone)
                {
                    img.RotateFlip(flip);
                    img.RemovePropertyItem(OrientationId);
                }
            }
            catch {}  // Ignorera
        }
    }
}
