using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using CvParser.Domain.Entities.CV.V1;
using CvParser.Domain.Common;

namespace CvParser.Infrastructure.Interfaces
{
    public interface ICvParserService
    {
        Task<Result<Cv?>> ParseCvFromUploadedFileAsync(IFormFile file);
    }
}

