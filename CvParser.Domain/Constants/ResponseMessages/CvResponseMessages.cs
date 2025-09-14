namespace CvParser.Domain.Constants.ResponseMessages;

public sealed class CvResponseMessages
{
    public const string NoFileSelected = "No file selected.";
    public const string NoFileUploaded = "No file uploaded.";
    public const string FileIsTooLarge = "File is too large. Maximum size is 10 MB.";
    public const string InvalidFileType = "Invalid file type. Only PDF and Word documents are allowed.";
    public const string FileUploadSuccess = "File uploaded successfully!";
    public const string NewXameraCvCreated = "New Xamera CV successfully";

    public const string ErrorUploadingFile = "Error uploading file:";
    public const string ErrorDownloadingFile = "Error Downloading / Creating new CV:";
    public const string ErrorOccurredDuringFileUpload = "An error occurred during file upload:";
    public const string ErrorOccurredDuringFileDownload = "An error occurred during new CV Creation:";
    public const string ErrorNewXameraCvCreated = "Failed to createa a new Xamera CV";


}
