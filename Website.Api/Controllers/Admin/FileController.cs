using Website.Api.Authorization;
using Website.Api.Models.Upload;
using Website.Domain.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Admin;

/// <summary>Tải ảnh sản phẩm lên (multipart/form-data, trường "file"). Trả về URL dùng cho mainImage / images.</summary>
[Authorize(Policy = AuthorizationPolicies.Management)]
public class FileController : WebsiteBaseController
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IWebHostEnvironment _env;

    public FileController(IWebHostEnvironment env) => _env = env;

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    public async Task<UploadFileOutput> UploadImage(IFormFile file)
    {
        if (file is not { Length: > 0 })
            throw new UserFriendlyException("Vui lòng chọn ảnh.");
        if (file.Length > MaxBytes)
            throw new UserFriendlyException("Ảnh không được vượt quá 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !file.ContentType.StartsWith("image/"))
            throw new UserFriendlyException("Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");

        // Tên file do server sinh để tránh ghi đè và path traversal.
        var month = DateTime.Now.ToString("yyyyMM");
        var folder = Path.Combine(_env.WebRootPath, "uploads", month);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
            await file.CopyToAsync(stream);

        return new UploadFileOutput { Url = $"/uploads/{month}/{fileName}" };
    }
}
