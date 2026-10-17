using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using HornViet.Application.Common;
using HornViet.Domain.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HornViet.Infrastructure.Services;

/// <summary>Lưu ảnh vào wwwroot/uploads/yyyyMM của máy chủ (môi trường dev); trả URL tương đối "/uploads/...".</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _webRootPath;

    public LocalFileStorage(string webRootPath) => _webRootPath = webRootPath;

    public async Task<string> SaveImageAsync(Stream content, string extension, CancellationToken ct = default)
    {
        // Tên file do server sinh để tránh ghi đè và path traversal.
        var month = DateTime.Now.ToString("yyyyMM");
        var folder = Path.Combine(_webRootPath, "uploads", month);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using (var stream = File.Create(Path.Combine(folder, fileName)))
            await content.CopyToAsync(stream, ct);

        return $"/uploads/{month}/{fileName}";
    }
}

/// <summary>Cấu hình Cloudinary (mục "Cloudinary"; trên server đặt bằng biến môi trường Cloudinary__Url, Cloudinary__Folder).</summary>
public class CloudinaryOptions
{
    /// <summary>
    /// "API environment variable" trên trang Cloudinary: cloudinary://API_KEY:API_SECRET@CLOUD_NAME — chứa khóa bí mật,
    /// chỉ đặt bằng biến môi trường, không ghi vào appsettings. Để trống = lưu ảnh trên máy chủ (LocalFileStorage).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>Thư mục trên Cloudinary — đặt khác nhau để tách ảnh staging và production.</summary>
    public string Folder { get; set; } = "hornviet";
}

/// <summary>Lưu ảnh lên Cloudinary (staging / production); trả URL tuyệt đối https://res.cloudinary.com/...</summary>
public class CloudinaryFileStorage : IFileStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly string _folder;
    private readonly ILogger<CloudinaryFileStorage> _logger;

    public CloudinaryFileStorage(IOptions<CloudinaryOptions> options, ILogger<CloudinaryFileStorage> logger)
    {
        _cloudinary = new Cloudinary(options.Value.Url);
        _cloudinary.Api.Secure = true; // URL ảnh dạng https
        _folder = options.Value.Folder;
        _logger = logger;
    }

    public async Task<string> SaveImageAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription($"{Guid.NewGuid():N}{extension}", content),
            Folder = _folder,
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        }, ct);

        if (result.Error != null || result.SecureUrl == null)
        {
            _logger.LogError("Tải ảnh lên Cloudinary thất bại: {Error}", result.Error?.Message);
            throw new UserFriendlyException("Không tải được ảnh lên. Vui lòng thử lại.");
        }

        return result.SecureUrl.ToString();
    }
}
