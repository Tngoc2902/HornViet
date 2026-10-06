using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Website.Api.Models;
using Website.Domain.SharedKernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Website.Api.Middlewares;

/// <summary>Bọc mọi kết quả thành công của controller vào AjaxResponse.</summary>
public class WrapResultFilter : IAsyncAlwaysRunResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        switch (context.Result)
        {
            case ObjectResult { Value: not AjaxResponse } objectResult:
                objectResult.Value = AjaxResponse.Ok(objectResult.Value);
                objectResult.DeclaredType = typeof(AjaxResponse);
                break;
            case EmptyResult or OkResult or NoContentResult:
                context.Result = new ObjectResult(AjaxResponse.Ok(null)) { StatusCode = StatusCodes.Status200OK };
                break;
        }
        return next();
    }
}

/// <summary>Chuyển exception thành AjaxResponse với HTTP status phù hợp.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, response) = ex switch
            {
                EntityNotFoundException nf => (StatusCodes.Status404NotFound, AjaxResponse.Fail(nf.Message, nf.Details)),
                UserFriendlyException uf => (StatusCodes.Status400BadRequest, AjaxResponse.Fail(uf.Message, uf.Details)),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, AjaxResponse.Fail(
                    "Dữ liệu (tồn kho) vừa được cập nhật bởi giao dịch khác. Vui lòng tải lại và thử lại.")),
                _ => (StatusCodes.Status500InternalServerError, AjaxResponse.Fail("Đã có lỗi xảy ra trong quá trình xử lý."))
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                _logger.LogInformation("Business error {Status} for {Path}: {Message}", status, context.Request.Path, ex.Message);

            await WriteAsync(context, status, response);
        }
    }

    // Giữ nguyên ký tự tiếng Việt thay vì escape \uXXXX.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static Task WriteAsync(HttpContext context, int status, AjaxResponse response)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}

public static class ValidationResponse
{
    /// <summary>Lỗi DataAnnotations (400) theo định dạng ABP: error.validationErrors[].</summary>
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .SelectMany(kv => kv.Value!.Errors.Select(e => new ValidationErrorInfo
            {
                Message = string.IsNullOrEmpty(e.ErrorMessage) ? "Giá trị không hợp lệ." : e.ErrorMessage,
                Members = new[] { JsonNamingPolicy.CamelCase.ConvertName(kv.Key.TrimStart('$', '.')) }
            }))
            .ToList();

        return new BadRequestObjectResult(AjaxResponse.Fail(
            "Dữ liệu không hợp lệ.", string.Join("\n", errors.Select(e => e.Message)), errors));
    }
}
