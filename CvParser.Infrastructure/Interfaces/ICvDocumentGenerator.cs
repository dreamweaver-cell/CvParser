using System.IO;
using System.Threading.Tasks;
using CvParser.Domain.Entities.CV.V1;
using CvParser.Domain.Common;

namespace CvParser.Infrastructure.Interfaces;

public interface ICvDocumentGenerator
{
    Task<Result<MemoryStream>> CreateXameraCVAsync(Cv cv, string locale);
    Task<Result<MemoryStream?>> ConvertDocxToPdfAsync(MemoryStream docxStream);
    
}
