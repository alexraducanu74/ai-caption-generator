namespace CaptionGenerator.API.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    public ApiResponse(T data, string message = "Success")
    {
        Success = true;
        Data = data;
        Message = message;
    }

    public ApiResponse(string message, Dictionary<string, string[]>? errors = null)
    {
        Success = false;
        Message = message;
        Errors = errors;
    }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Success") =>
        new(data, message);

    public static ApiResponse<T> ErrorResponse(string message, Dictionary<string, string[]>? errors = null) =>
        new(message, errors);
}

