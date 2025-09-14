using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CvParser.Infrastructure.Interfaces
{
    public interface IPdfService
    {
        /// Extraherar all text från en PDF-ström.
        /// <param name="pdfStream">Stream som innehåller PDF-filen.</param>
        /// <returns>Textinnehållet som string.</returns>
        string GetText(Stream pdfStream);

        /// Extraherar alla bilder från en PDF-ström.
        /// <param name="pdfStream">Stream som innehåller PDF-filen.</param>
        /// <returns>Lista med bilder från PDF:en.</returns>
        List<Image> GetImages(Stream pdfStream);
    }
}