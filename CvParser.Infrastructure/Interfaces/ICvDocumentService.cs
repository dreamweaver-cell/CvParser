using System.IO;
using System.Threading.Tasks;
using CvParser.Domain.Entities.CV.V1;

namespace CvParser.Infrastructure.Interfaces
{
    public interface ICvDocumentService
    {
        Task<MemoryStream> CreateCvDocumentAsync(Cv cv);
    }
}
