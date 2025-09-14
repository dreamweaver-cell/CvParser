using System.IO;
using System.Threading.Tasks;

namespace CvParser.Infrastructure.Interfaces
{
    public interface IDocxService
    {
        /// Asynkront extraherar all text från en DOCX-filström.
        /// <param name="fileStream">Filströmmen för DOCX-filen.</param>
        /// <returns>En sträng som innehåller den extraherade texten.</returns>
        Task<string> ExtractTextFromDocxAsync(Stream fileStream);

        string GetText(Stream fileStream);

        public List<Image> GetImages(Stream fileStream);
    }

}
