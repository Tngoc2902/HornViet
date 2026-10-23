namespace HornViet.Domain.Chat.Enums;

/// <summary>Việc bot làm thêm sau khi nhận ra ý định, ngoài câu trả lời mẫu (tra dữ liệu thật trong hệ thống).</summary>
public enum ChatIntentAction
{
    None = 0,           // Chỉ trả lời theo mẫu (chính sách, địa chỉ, giờ mở cửa...)
    SearchProducts = 1, // Tìm sản phẩm theo từ khóa trong câu
    ProductPrice = 2,   // Báo giá sản phẩm khách nhắc tới
    ProductStock = 3,   // Báo còn hàng theo từng biến thể
    Collections = 4,    // Liệt kê bộ sưu tập đang bật
    MyOrders = 5,       // Tra đơn của khách đã đăng nhập
    Handoff = 6         // Gợi ý chuyển sang chat với nhân viên
}

/// <summary>Trạng thái hội thoại hỗ trợ. Chuyển trạng thái do ChatConversation kiểm soát.</summary>
public enum ChatConversationStatus
{
    Bot = 0,          // Bot đang trả lời
    WaitingStaff = 1, // Khách đã bấm "Chat với nhân viên", chờ nhân viên tiếp nhận
    WithStaff = 2,    // Nhân viên đang trả lời trực tiếp
    Closed = 3        // Đã kết thúc
}

/// <summary>Người gửi một tin nhắn.</summary>
public enum ChatSender
{
    Customer = 0,
    Bot = 1,
    Staff = 2,
    System = 3 // Thông báo của hệ thống: "Đang chờ nhân viên", "Nhân viên đã tham gia"...
}
