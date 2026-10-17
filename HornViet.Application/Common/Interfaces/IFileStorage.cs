namespace HornViet.Application.Common;

/// <summary>
/// Lưu ảnh sản phẩm / nguyên liệu do quản trị tải lên. Interface ở tầng Application, hai cài đặt ở tầng Infrastructure:
/// LocalFileStorage (thư mục wwwroot/uploads của máy chủ — môi trường dev) và CloudinaryFileStorage (dịch vụ lưu ảnh
/// trên cloud — môi trường staging / production, nơi ổ đĩa máy chủ bị xóa mỗi lần deploy).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Lưu ảnh với tên do hệ thống sinh, trả về URL để ghi vào mainImage / images: dạng tương đối "/uploads/..." khi lưu
    /// trên máy chủ, dạng tuyệt đối "https://..." khi lưu trên Cloudinary (frontend xử lý được cả hai).
    /// </summary>
    /// <param name="content">Nội dung ảnh.</param>
    /// <param name="extension">Phần mở rộng đã kiểm tra, VD ".jpg".</param>
    Task<string> SaveImageAsync(Stream content, string extension, CancellationToken ct = default);
}
