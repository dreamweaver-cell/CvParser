using System.Threading.Tasks;
using CvParser.Domain.Entities.CV;

namespace CvParser.Infrastructure.Interfaces;

public interface IAiService
{
    Task<string> GenerateContentAsync(string prompt);
    Task<string> SendCvAsync(string cvText);
}
