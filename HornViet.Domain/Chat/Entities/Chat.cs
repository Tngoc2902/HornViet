using HornViet.Domain.Chat.Enums;
using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.Chat.Entities;

/// <summary>
/// Một ý định (intent) của khách mà chatbot nhận diện được, VD "hoi_gia" = hỏi giá sản phẩm.
/// Mỗi ý định có các câu mẫu để huấn luyện model và một câu trả lời mẫu.
/// </summary>
public class ChatIntent : Entity<int>, IHasCreationTime
{
    /// <summary>Mã ý định — nhãn khi huấn luyện (chữ thường, không dấu, nối bằng "_").</summary>
    public string Code { get; set; } = null!;
    /// <summary>Tên hiển thị ở trang quản trị.</summary>
    public string Name { get; set; } = null!;
    /// <summary>Câu trả lời mẫu bot gửi khi nhận ra ý định này.</summary>
    public string Answer { get; set; } = null!;
    /// <summary>Nút link kèm câu trả lời (VD "Xem chính sách đổi trả" → /ho-tro#doi-tra); để trống thì không có.</summary>
    public string? LinkLabel { get; set; }
    public string? LinkUrl { get; set; }
    public ChatIntentAction Action { get; set; }
    /// <summary>Tắt thì ý định không được đưa vào lần huấn luyện kế tiếp.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreationTime { get; set; }

    public List<ChatPhrase> Phrases { get; set; } = new();
}

/// <summary>Câu mẫu (dữ liệu huấn luyện) của một ý định.</summary>
public class ChatPhrase : Entity<int>
{
    public int IntentId { get; set; }
    public string Text { get; set; } = null!;
}

/// <summary>
/// Kết quả một lần huấn luyện: file model ML.NET và độ chính xác đo trên tập kiểm tra.
/// Bản mới nhất là model bot đang dùng. Lưu trong CSDL vì ổ đĩa máy chủ cloud bị xóa mỗi lần deploy.
/// </summary>
public class ChatModel : Entity<int>, IHasCreationTime
{
    public byte[] Data { get; set; } = null!;
    /// <summary>Dấu vân tay (SHA-256) của bộ câu mẫu lúc huấn luyện — khác bộ hiện tại nghĩa là cần huấn luyện lại.</summary>
    public string DataHash { get; set; } = null!;
    public int IntentCount { get; set; }
    public int PhraseCount { get; set; }
    /// <summary>Tỉ lệ câu đoán đúng trên tập kiểm tra (micro accuracy).</summary>
    public double Accuracy { get; set; }
    /// <summary>Trung bình độ chính xác của từng ý định (macro accuracy) — không bị ý định nhiều câu mẫu lấn át.</summary>
    public double MacroAccuracy { get; set; }
    /// <summary>Báo cáo chi tiết (JSON): chỉ số từng ý định, các cặp hay nhầm, cảnh báo dữ liệu.</summary>
    public string Report { get; set; } = null!;
    public long? CreatorUserId { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>
/// Một cuộc trò chuyện hỗ trợ: bot trả lời trước, khách bấm "Chat với nhân viên" thì chuyển cho nhân viên.
/// Khách đã đăng nhập gắn với tài khoản; khách vãng lai giữ khóa bí mật <see cref="GuestKey"/> do máy chủ cấp.
/// </summary>
public class ChatConversation : Entity<int>, IHasCreationTime
{
    public long? UserId { get; private set; }
    public string? GuestKey { get; private set; }
    public ChatConversationStatus Status { get; private set; }
    /// <summary>Nhân viên đang phụ trách (sau khi tiếp nhận).</summary>
    public long? StaffUserId { get; private set; }
    /// <summary>Sản phẩm khách vừa hỏi — để câu hỏi tiếp theo như "còn hàng không?" hiểu là hỏi về sản phẩm đó.</summary>
    public int? LastProductId { get; set; }
    /// <summary>Số tin của khách nhân viên chưa đọc (chỉ đếm khi hội thoại đã chuyển cho nhân viên).</summary>
    public int StaffUnread { get; private set; }
    public DateTime LastMessageTime { get; private set; }
    public DateTime CreationTime { get; set; }

    public List<ChatMessage> Messages { get; private set; } = new();

    private ChatConversation() { }

    public ChatConversation(long? userId, string? guestKey)
    {
        if (userId is null && string.IsNullOrEmpty(guestKey))
            throw new ArgumentException("Hội thoại của khách vãng lai cần khóa truy cập.", nameof(guestKey));
        UserId = userId;
        GuestKey = userId is null ? guestKey : null;
        Status = ChatConversationStatus.Bot;
        LastMessageTime = DateTime.Now;
    }

    public bool IsClosed => Status == ChatConversationStatus.Closed;

    /// <summary>Đã chuyển cho nhân viên (đang chờ hoặc đang trả lời): bot không tự trả lời nữa.</summary>
    public bool IsHandedOff => Status is ChatConversationStatus.WaitingStaff or ChatConversationStatus.WithStaff;

    public ChatMessage AddMessage(ChatSender sender, string content, long? staffUserId = null)
    {
        if (IsClosed)
            throw new UserFriendlyException("Cuộc trò chuyện đã kết thúc. Hãy bắt đầu cuộc trò chuyện mới.");
        var message = new ChatMessage
        {
            Sender = sender,
            Content = content,
            StaffUserId = staffUserId,
            CreationTime = DateTime.Now
        };
        Messages.Add(message);
        LastMessageTime = message.CreationTime;
        if (sender == ChatSender.Customer && IsHandedOff)
            StaffUnread++;
        return message;
    }

    /// <summary>Khách bấm "Chat với nhân viên": Bot → WaitingStaff. Bấm lại khi đã chuyển thì không đổi gì.</summary>
    public void RequestStaff()
    {
        EnsureOpen();
        if (Status == ChatConversationStatus.Bot)
            Status = ChatConversationStatus.WaitingStaff;
    }

    /// <summary>Khách hủy yêu cầu khi chưa có nhân viên nào tiếp nhận: WaitingStaff → Bot.</summary>
    public void CancelStaffRequest()
    {
        EnsureOpen();
        if (Status == ChatConversationStatus.WithStaff)
            throw new UserFriendlyException("Nhân viên đã tiếp nhận cuộc trò chuyện.");
        Status = ChatConversationStatus.Bot;
        StaffUnread = 0;
    }

    /// <summary>
    /// Nhân viên tiếp nhận (kể cả khi khách chưa bấm yêu cầu — chủ động hỗ trợ). Nhân viên khác đã nhận thì phải
    /// <paramref name="takeOver"/> = true mới giành lại được, tránh hai người cùng trả lời một khách.
    /// </summary>
    public void AssignStaff(long staffUserId, bool takeOver = false)
    {
        EnsureOpen();
        if (Status == ChatConversationStatus.WithStaff && StaffUserId != staffUserId && !takeOver)
            throw new UserFriendlyException("Cuộc trò chuyện đã có nhân viên khác tiếp nhận.");
        Status = ChatConversationStatus.WithStaff;
        StaffUserId = staffUserId;
    }

    public void MarkReadByStaff() => StaffUnread = 0;

    public void Close()
    {
        Status = ChatConversationStatus.Closed;
        StaffUnread = 0;
    }

    private void EnsureOpen()
    {
        if (IsClosed)
            throw new UserFriendlyException("Cuộc trò chuyện đã kết thúc. Hãy bắt đầu cuộc trò chuyện mới.");
    }
}

/// <summary>Một tin nhắn trong hội thoại hỗ trợ.</summary>
public class ChatMessage : Entity<long>, IHasCreationTime
{
    public int ConversationId { get; set; }
    public ChatSender Sender { get; set; }
    public string Content { get; set; } = null!;
    /// <summary>Tin của bot: dữ liệu kèm theo dạng JSON (thẻ sản phẩm, đơn hàng, link, gợi ý câu hỏi).</summary>
    public string? Payload { get; set; }
    /// <summary>Tin của khách: ý định bot đoán và độ tin cậy (0–1).</summary>
    public string? IntentCode { get; set; }
    public double? Confidence { get; set; }
    /// <summary>Bot không chắc chắn → câu này nằm trong danh sách "Câu bot chưa hiểu" chờ gán nhãn.</summary>
    public bool NeedsReview { get; set; }
    public long? StaffUserId { get; set; }
    public DateTime CreationTime { get; set; }
}
