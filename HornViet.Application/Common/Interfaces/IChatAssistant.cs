namespace HornViet.Application.Common;

/// <summary>
/// Trợ lý AI bên ngoài (mô hình ngôn ngữ lớn) trả lời những câu mà model tự huấn luyện chưa hiểu.
/// Interface ở tầng Application, cài đặt bằng Gemini API ở tầng Infrastructure (Services/GeminiChatAssistant).
/// Chưa cấu hình khóa API thì <see cref="IsEnabled"/> = false và bot chỉ dùng model tự huấn luyện.
/// </summary>
/// <remarks>
/// Nội dung gửi đi đã được che thông tin cá nhân (số điện thoại, email, dãy số dài) và không kèm tên, địa chỉ hay
/// đơn hàng của khách. Lỗi mạng / hết lượt gọi / câu trả lời hỏng → trả null, bot quay về câu trả lời "chưa hiểu".
/// </remarks>
public interface IChatAssistant
{
    bool IsEnabled { get; }

    Task<AssistantReply?> AskAsync(AssistantRequest request, CancellationToken ct = default);
}

/// <param name="Instructions">Quy tắc và thông tin cửa hàng (chính sách, bộ sưu tập, danh sách sản phẩm).</param>
/// <param name="History">Vài tin gần nhất trong hội thoại (đã che thông tin cá nhân), cũ trước.</param>
/// <param name="Question">Câu khách vừa hỏi (đã che thông tin cá nhân).</param>
public record AssistantRequest(string Instructions, IReadOnlyList<AssistantTurn> History, string Question);

public record AssistantTurn(bool FromCustomer, string Text);

/// <param name="ProductIds">Mã sản phẩm trong danh sách đã gửi mà câu trả lời nhắc tới (hiện thành thẻ sản phẩm).</param>
/// <param name="SuggestStaff">AI không chắc / khách cần người thật → hiện nút "Chat với nhân viên".</param>
public record AssistantReply(string Answer, IReadOnlyList<int> ProductIds, bool SuggestStaff);
