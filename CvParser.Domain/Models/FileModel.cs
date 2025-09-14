using CvParser.Domain.Enums;
using Microsoft.AspNetCore.Components.Forms;

namespace CvParser.Domain.Models;

public class FileModel
{
    public IBrowserFile File { get; set; }
    public DocumentType DocumentType { get; set; }

}