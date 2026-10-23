using System.ComponentModel.DataAnnotations;
using HornViet.Application.Common;
using HornViet.Domain;
using HornViet.Domain.Chat.Enums;
using HornViet.Domain.Orders.Enums;

namespace HornViet.Application.Chat;

// ------------------------------------------------------------------ Tin nhắn

/// <summary>Thẻ sản phẩm trong câu trả lời của bot (bấm để mở trang sản phẩm).</summary>
public class ChatProductCardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string MainImage { get; set; } = null!;
    public decimal Price { get; set; }
    public bool InStock { get; set; }
    /// <summary>Tình trạng từng biến thể (chỉ có khi khách hỏi còn hàng), VD "Cỡ 58: còn 3".</summary>
    public List<string>? Variants { get; set; }
}

/// <summary>Đơn hàng của chính khách đang chat (khách đã đăng nhập hỏi "đơn của tôi đâu").</summary>
public class ChatOrderCardDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public OrderStatus Status { get; set; }
    public DeliveryMethod DeliveryMethod { get; set; }
    public decimal PayableAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreationTime { get; set; }
}

public class ChatLinkDto
{
    public string Label { get; set; } = null!;
    /// <summary>Đường dẫn trong trang khách, VD "/ho-tro#doi-tra".</summary>
    public string Url { get; set; } = null!;
}

/// <summary>Phần dữ liệu kèm tin nhắn của bot (lưu JSON ở ChatMessage.Payload).</summary>
public class ChatPayload
{
    public List<ChatProductCardDto>? Products { get; set; }
    public List<ChatOrderCardDto>? Orders { get; set; }
    public List<ChatLinkDto>? Links { get; set; }
    /// <summary>Câu hỏi gợi ý — khách bấm là gửi luôn.</summary>
    public List<string>? QuickReplies { get; set; }
    /// <summary>Bot gợi ý chuyển sang nhân viên (hiện nút "Chat với nhân viên" nổi bật ngay dưới tin).</summary>
    public bool SuggestStaff { get; set; }
    /// <summary>Câu trả lời do trợ lý AI (Gemini) soạn — giao diện ghi chú "có thể chưa chính xác".</summary>
    public bool Ai { get; set; }
}

public class ChatMessageDto : ChatPayload
{
    public long Id { get; set; }
    public ChatSender Sender { get; set; }
    public string Content { get; set; } = null!;
    /// <summary>Tên nhân viên (tin của nhân viên).</summary>
    public string? StaffName { get; set; }
    public DateTime CreationTime { get; set; }
}

// ------------------------------------------------------------------ Trang khách

/// <summary>
/// Xác định hội thoại: khách đã đăng nhập dùng tài khoản (ConversationId có thể bỏ trống = hội thoại đang mở gần nhất);
/// khách vãng lai gửi kèm GuestKey nhận được lúc bắt đầu.
/// </summary>
public class ChatAccessInput
{
    public int? ConversationId { get; set; }

    [StringLength(64)]
    public string? GuestKey { get; set; }
}

public class SendChatInput : ChatAccessInput
{
    [Required(ErrorMessage = "Vui lòng nhập tin nhắn."), StringLength(AppConsts.ChatMaxMessageLength, ErrorMessage = "Tin nhắn quá dài.")]
    public string Text { get; set; } = null!;
}

public class ChatConversationDto
{
    public int Id { get; set; }
    public ChatConversationStatus Status { get; set; }
    /// <summary>Chỉ trả cho khách vãng lai sở hữu hội thoại — giao diện lưu lại để mở lại sau.</summary>
    public string? GuestKey { get; set; }
    /// <summary>Đang có nhân viên / quản trị viên mở trang quản trị (để báo khách "nhân viên sẽ trả lời ngay").</summary>
    public bool StaffOnline { get; set; }
    public string? StaffName { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
    public DateTime CreationTime { get; set; }
}

/// <summary>Kết quả gửi tin: hội thoại (có thể vừa được tạo) và các tin mới — tin của khách và trả lời của bot.</summary>
public class ChatSendResultDto
{
    public int ConversationId { get; set; }
    public ChatConversationStatus Status { get; set; }
    public string? GuestKey { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

// ------------------------------------------------------------------ Trang quản trị: hỗ trợ khách

public class GetChatConversationsInput : PagedInput
{
    /// <summary>Bỏ trống = các hội thoại đang chờ hoặc đang hỗ trợ.</summary>
    public ChatConversationStatus? Status { get; set; }
}

public class ChatConversationListItemDto
{
    public int Id { get; set; }
    public ChatConversationStatus Status { get; set; }
    /// <summary>Tên tài khoản khách; khách vãng lai: "Khách vãng lai".</summary>
    public string CustomerName { get; set; } = null!;
    public bool IsGuest { get; set; }
    public long? StaffUserId { get; set; }
    public string? StaffName { get; set; }
    public string? LastMessage { get; set; }
    public ChatSender? LastSender { get; set; }
    public int StaffUnread { get; set; }
    public int MessageCount { get; set; }
    public DateTime LastMessageTime { get; set; }
    public DateTime CreationTime { get; set; }
}

public class ChatSupportConversationDto : ChatConversationDto
{
    public string CustomerName { get; set; } = null!;
    public bool IsGuest { get; set; }
    public long? StaffUserId { get; set; }
    /// <summary>Nhân viên đang xem có phải người phụ trách không (để hiện ô trả lời / nút "Tiếp nhận").</summary>
    public bool IsMine { get; set; }
}

public class StaffReplyInput
{
    public int ConversationId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tin nhắn."), StringLength(AppConsts.ChatMaxMessageLength, ErrorMessage = "Tin nhắn quá dài.")]
    public string Text { get; set; } = null!;
}

public class ClaimChatInput
{
    public int ConversationId { get; set; }
    /// <summary>Giành lại hội thoại nhân viên khác đang phụ trách.</summary>
    public bool TakeOver { get; set; }
}

public class ChatSupportSummaryDto
{
    public int WaitingCount { get; set; }
    public int MineUnread { get; set; }
}

// ------------------------------------------------------------------ Trang quản trị: huấn luyện

public class ChatIntentDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public string? LinkLabel { get; set; }
    public string? LinkUrl { get; set; }
    public ChatIntentAction Action { get; set; }
    public bool IsActive { get; set; }
    public int PhraseCount { get; set; }
    /// <summary>Chỉ có khi xem chi tiết.</summary>
    public List<string>? Phrases { get; set; }
}

public class CreateOrUpdateChatIntentInput
{
    public int? Id { get; set; }

    /// <summary>Để trống thì tạo từ tên ("Hỏi giá" → "hoi_gia").</summary>
    [StringLength(64)]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên ý định."), StringLength(128)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập câu trả lời mẫu."), StringLength(2000)]
    public string Answer { get; set; } = null!;

    [StringLength(64)]
    public string? LinkLabel { get; set; }

    [StringLength(300)]
    public string? LinkUrl { get; set; }

    public ChatIntentAction Action { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Câu mẫu, mỗi phần tử một câu. Câu trùng nhau (sau khi bỏ dấu) chỉ giữ một.</summary>
    public List<string> Phrases { get; set; } = new();
}

public class ChatModelDto
{
    public int Id { get; set; }
    public DateTime CreationTime { get; set; }
    public int IntentCount { get; set; }
    public int PhraseCount { get; set; }
    public double Accuracy { get; set; }
    public double MacroAccuracy { get; set; }
    public int TrainCount { get; set; }
    public int TestCount { get; set; }
    public List<IntentMetricDto> PerIntent { get; set; } = new();
    public List<IntentConfusionDto> Confusions { get; set; } = new();
    /// <summary>Cảnh báo dữ liệu, VD một câu mẫu nằm ở hai ý định.</summary>
    public List<string> Warnings { get; set; } = new();
    /// <summary>Câu mẫu đã đổi sau lần huấn luyện này → nên huấn luyện lại.</summary>
    public bool IsOutdated { get; set; }
    /// <summary>Model này đang được bot dùng.</summary>
    public bool IsLoaded { get; set; }
}

public class IntentMetricDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Support { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
}

public class IntentConfusionDto
{
    public string Actual { get; set; } = null!;
    public string Predicted { get; set; } = null!;
    public int Count { get; set; }
}

public class ChatUnrecognizedDto
{
    public long MessageId { get; set; }
    /// <summary>Đã che số điện thoại, email.</summary>
    public string Text { get; set; } = null!;
    public string? IntentCode { get; set; }
    public double? Confidence { get; set; }
    public DateTime CreationTime { get; set; }
}

public class LabelChatMessageInput
{
    public long MessageId { get; set; }
    public int IntentId { get; set; }
}

public class TestChatInput
{
    [Required, StringLength(AppConsts.ChatMaxMessageLength)]
    public string Text { get; set; } = null!;
}

public class ChatTestResultDto
{
    public string Normalized { get; set; } = null!;
    public string? IntentCode { get; set; }
    public string? IntentName { get; set; }
    public double? Confidence { get; set; }
    /// <summary>Độ tin cậy dưới ngưỡng → bot sẽ trả lời "chưa hiểu".</summary>
    public bool BelowThreshold { get; set; }
    public double Threshold { get; set; }
    public ChatMessageDto Reply { get; set; } = null!;
}
