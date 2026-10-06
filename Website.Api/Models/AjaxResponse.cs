using System.Text.Json.Serialization;

namespace Website.Api.Models;

/// <summary>
/// Vỏ bọc response thống nhất theo định dạng ABP để frontend xử lý một kiểu duy nhất:
/// { result, targetUrl, success, error, unauthorizedRequest, __abp }.
/// </summary>
public class AjaxResponse
{
    public object? Result { get; set; }
    public string? TargetUrl { get; set; }
    public bool Success { get; set; }
    public ErrorInfo? Error { get; set; }
    public bool UnauthorizedRequest { get; set; }

    [JsonPropertyName("__abp")]
    public bool Abp { get; set; } = true;

    public static AjaxResponse Ok(object? result) => new() { Result = result, Success = true };

    public static AjaxResponse Fail(string message, string? details = null,
        IReadOnlyList<ValidationErrorInfo>? validationErrors = null, bool unauthorized = false) => new()
    {
        Success = false,
        UnauthorizedRequest = unauthorized,
        Error = new ErrorInfo { Message = message, Details = details, ValidationErrors = validationErrors }
    };
}

public class ErrorInfo
{
    public int Code { get; set; }
    public string Message { get; set; } = null!;
    public string? Details { get; set; }
    public IReadOnlyList<ValidationErrorInfo>? ValidationErrors { get; set; }
}

public class ValidationErrorInfo
{
    public string Message { get; set; } = null!;
    public IReadOnlyList<string> Members { get; set; } = Array.Empty<string>();
}
