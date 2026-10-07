// File thuộc SharedKernel: định nghĩa các lớp ngoại lệ (exception) dùng để báo lỗi nghiệp vụ từ tầng Domain
// và Application. Tầng Domain chỉ "ném" lỗi (throw); việc đổi lỗi thành mã HTTP do middleware ResponseHandling
// ở tầng Api đảm nhiệm — Domain không cần biết gì về HTTP.
namespace Website.Domain.SharedKernel;

// Kế thừa Exception (lớp ngoại lệ gốc của .NET) nên có thể dùng với throw / try-catch như mọi ngoại lệ khác.
// Phân biệt lỗi nghiệp vụ (do người dùng nhập sai, hết hàng...) với lỗi hệ thống (bug, mất kết nối CSDL):
// lỗi nghiệp vụ trả HTTP 400 kèm thông điệp tiếng Việt; lỗi hệ thống trả HTTP 500 với thông điệp chung
// và được ghi log, tránh lộ chi tiết kỹ thuật ra ngoài.
/// <summary>
/// Lỗi nghiệp vụ có thông điệp hiển thị được cho người dùng (tương đương UserFriendlyException của ABP).
/// Middleware trả về HTTP 400 kèm thông điệp này.
/// </summary>
public class UserFriendlyException : Exception
{
    /// <summary>
    /// Thông tin chi tiết bổ sung (có thể null), VD danh sách từng sản phẩm không đủ hàng khi đặt hàng.
    /// </summary>
    // string?: dấu ? cho phép giá trị null (nullable reference type).
    // Chỉ có get, không có set: giá trị chỉ được gán một lần trong constructor, sau đó không đổi được.
    public string? Details { get; }

    /// <summary>Tạo lỗi nghiệp vụ với thông điệp chính và chi tiết tùy chọn.</summary>
    /// <param name="message">Thông điệp chính hiển thị cho người dùng.</param>
    /// <param name="details">Chi tiết bổ sung, mặc định null nếu không truyền.</param>
    // ": base(message)" gọi constructor của lớp cha Exception để lưu thông điệp vào thuộc tính Message.
    public UserFriendlyException(string message, string? details = null) : base(message)
    {
        // Lưu phần chi tiết để middleware trả kèm trong phản hồi JSON.
        Details = details;
    }
}

// Kế thừa UserFriendlyException nên vẫn là lỗi nghiệp vụ, nhưng middleware nhận diện riêng để trả HTTP 404
// (thay vì 400). Middleware kiểm tra EntityNotFoundException trước UserFriendlyException, vì lớp con cũng
// khớp với điều kiện của lớp cha.
/// <summary>Không tìm thấy tài nguyên — middleware trả về HTTP 404.</summary>
public class EntityNotFoundException : UserFriendlyException
{
    /// <summary>Tạo lỗi "không tìm thấy" với thông điệp cho người dùng, VD "Không tìm thấy đơn hàng.".</summary>
    /// <param name="message">Thông điệp hiển thị cho người dùng.</param>
    // Thân constructor rỗng { }: mọi việc đã được chuyển cho lớp cha qua base(message).
    public EntityNotFoundException(string message) : base(message) { }
}
