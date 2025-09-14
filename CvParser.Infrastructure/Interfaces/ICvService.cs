using System.IO;
using System.Threading.Tasks;
using Cv = CvParser.Domain.Entities.CV.V1.Cv;
using Microsoft.AspNetCore.Http;
using CvParser.Domain.Common;

namespace CvParser.Infrastructure.Interfaces
{
    public interface ICvService
    {
        Task<Result<Cv?>> ParseCvAsync(IFormFile file);
    }
}
