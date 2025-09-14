namespace CvParser.Domain.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public T? Data { get; init; }

        private ApiResponse(bool success, T? data, string? error)
        {
            Success = success;
            Data = data;
            Error = error;
        }

        public static ApiResponse<T> SuccessResponse(T data) =>
            new ApiResponse<T>(true, data, null);

        public static ApiResponse<T> FailureResponse(string error) =>
            new ApiResponse<T>(false, default, error);
    }
}
