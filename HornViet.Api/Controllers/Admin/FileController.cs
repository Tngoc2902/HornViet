using HornViet.Api.Authorization;
using HornViet.Api.Models.Upload;
using HornViet.Application.Common;
using HornViet.Domain.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Admin;

/// <summary>Tải ảnh sản phẩm lên (multipart/form-data, trường "file"). Trả về URL dùng cho mainImage / images.</summary>
[Authorize(Policy = AuthorizationPolicies.Management)]
public class FileController : HornVietBaseController
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IFileStorage _storage;

    public FileController(IFileStorage storage) => _storage = storage;

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    public Task<ActionResult<UploadFileOutput>> UploadImage(IFormFile file) => Run(() => SaveImageAsync(file));

    private async Task<UploadFileOutput> SaveImageAsync(IFormFile file)
    {
        if (file is not { Length: > 0 })
            throw new UserFriendlyException("Vui lòng chọn ảnh.");
        if (file.Length > MaxBytes)
            throw new UserFriendlyException("Ảnh không được vượt quá 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !file.ContentType.StartsWith("image/"))
            throw new UserFriendlyException("Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");

        // Lưu trên máy chủ (dev) hoặc Cloudinary (staging / production) tùy cấu hình — xem IFileStorage.
        await using var stream = file.OpenReadStream();
        return new UploadFileOutput { Url = await _storage.SaveImageAsync(stream, extension, HttpContext.RequestAborted) };
    }
}
