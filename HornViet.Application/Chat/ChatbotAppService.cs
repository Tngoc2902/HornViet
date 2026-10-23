using System.Security.Cryptography;
using System.Text;
using HornViet.Application.Common;
using HornViet.Domain.Chat.Entities;
using HornViet.Domain.Chat.Enums;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Chat;

/// <summary>
/// Ô chat ở trang khách: bot trả lời tự động; khách bấm "Chat với nhân viên" thì tin nhắn chuyển cho nhân viên trả lời
/// trực tiếp (thời gian thực qua SignalR). Dùng được cả khi chưa đăng nhập.
/// </summary>
public interface IChatbotAppService
{
    /// <summary>Hội thoại đang mở của khách (null = chưa có; giao diện hiện lời chào, tin đầu tiên sẽ tạo hội thoại).</summary>
    Task<ChatConversationDto?> GetAsync(ChatAccessInput input);

    /// <summary>Gửi tin: chưa chuyển cho nhân viên thì bot trả lời ngay; đã chuyển thì báo cho nhân viên.</summary>
    Task<ChatSendResultDto> SendAsync(SendChatInput input);

    /// <summary>Nút "Chat với nhân viên".</summary>
    Task<ChatConversationDto> RequestStaffAsync(ChatAccessInput input);

    /// <summary>Hủy yêu cầu khi chưa có nhân viên tiếp nhận — quay lại trò chuyện với bot.</summary>
    Task<ChatConversationDto> CancelStaffAsync(ChatAccessInput input);

    /// <summary>Khách kết thúc cuộc trò chuyện.</summary>
    Task CloseAsync(ChatAccessInput input);

    /// <summary>Kết nối SignalR được nghe tin của hội thoại không (chủ hội thoại, hoặc nhân viên / quản trị viên).</summary>
    Task<bool> CanJoinAsync(int conversationId, string? guestKey, long? userId, bool isManagement);
}

public class ChatbotAppService : IChatbotAppService
{
    private const int HistoryLimit = 50;

    private readonly IRepository<ChatConversation, int> _conversationRepository;
    private readonly IRepository<ChatMessage, long> _messageRepository;
    private readonly IRepository<User, long> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;
    private readonly ChatBot _bot;
    private readonly IRealtimeNotifier _notifier;
    private readonly IStaffPresence _presence;

    public ChatbotAppService(IRepository<ChatConversation, int> conversationRepository, IRepository<ChatMessage, long> messageRepository,
        IRepository<User, long> userRepository, IUnitOfWork unitOfWork, IAppSession session, ChatBot bot,
        IRealtimeNotifier notifier, IStaffPresence presence)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _session = session;
        _bot = bot;
        _notifier = notifier;
        _presence = presence;
    }

    public async Task<ChatConversationDto?> GetAsync(ChatAccessInput input)
    {
        var conversation = await FindAsync(input);
        return conversation is null ? null : await ToDtoAsync(conversation, input.GuestKey);
    }

    public async Task<ChatSendResultDto> SendAsync(SendChatInput input)
    {
        var text = input.Text.Trim();
        if (text.Length == 0)
            throw new UserFriendlyException("Vui lòng nhập tin nhắn.");

        var (conversation, guestKey) = await FindOrCreateAsync(input);
        var history = conversation.Id == 0 || conversation.IsHandedOff
            ? []
            : await _messageRepository.GetAll().AsNoTracking()
                .Where(m => m.ConversationId == conversation.Id && (m.Sender == ChatSender.Customer || m.Sender == ChatSender.Bot))
                .OrderByDescending(m => m.Id)
                .Take(ChatBot.AssistantHistory)
                .Select(m => new AssistantTurn(m.Sender == ChatSender.Customer, m.Content))
                .ToListAsync();
        history.Reverse();
        var customerMessage = conversation.AddMessage(ChatSender.Customer, text);
        var added = new List<ChatMessage> { customerMessage };

        if (!conversation.IsHandedOff)
        {
            var reply = await _bot.ReplyAsync(text, _session.UserId, conversation.LastProductId, history);
            customerMessage.IntentCode = reply.Prediction?.Label;
            customerMessage.Confidence = reply.Prediction is { } p ? Math.Round(p.Confidence, 4) : null;
            customerMessage.NeedsReview = !reply.Understood;
            if (reply.ProductId is { } productId)
                conversation.LastProductId = productId;
            var botMessage = conversation.AddMessage(ChatSender.Bot, reply.Content);
            botMessage.Payload = ChatMapping.SerializePayload(reply.Payload);
            added.Add(botMessage);
        }
        await _unitOfWork.SaveChangesAsync();

        var dtos = added.Select(m => ChatMapping.ToDto(m, new Dictionary<long, string>())).ToList();
        foreach (var dto in dtos)
            await _notifier.ChatMessageAsync(conversation.Id, dto);
        if (conversation.IsHandedOff)
            await NotifyChangedAsync(conversation);

        return new ChatSendResultDto
        {
            ConversationId = conversation.Id,
            Status = conversation.Status,
            GuestKey = guestKey,
            Messages = dtos
        };
    }

    public async Task<ChatConversationDto> RequestStaffAsync(ChatAccessInput input)
    {
        var (conversation, guestKey) = await FindOrCreateAsync(input);
        if (!conversation.IsHandedOff)
        {
            conversation.RequestStaff();
            var text = _presence.AnyStaffOnline()
                ? "Đã báo nhân viên. Bạn chờ trong giây lát, nhân viên sẽ trả lời ngay tại đây nhé."
                : "Hiện chưa có nhân viên trực tuyến. Bạn cứ để lại câu hỏi, nhân viên sẽ trả lời tại đây ngay khi có mặt " +
                  "(giờ làm việc 8h00 – 21h00).";
            await AddSystemMessageAsync(conversation, text);
        }
        return await ToDtoAsync(conversation, guestKey);
    }

    public async Task<ChatConversationDto> CancelStaffAsync(ChatAccessInput input)
    {
        var conversation = await FindAsync(input) ?? throw new EntityNotFoundException("Không tìm thấy cuộc trò chuyện.");
        if (conversation.Status == ChatConversationStatus.WaitingStaff)
        {
            conversation.CancelStaffRequest();
            await AddSystemMessageAsync(conversation, "Bạn đã quay lại trò chuyện với trợ lý tự động.");
        }
        return await ToDtoAsync(conversation, input.GuestKey);
    }

    public async Task CloseAsync(ChatAccessInput input)
    {
        var conversation = await FindAsync(input);
        if (conversation is null || conversation.IsClosed)
            return;
        var message = conversation.AddMessage(ChatSender.System, "Cuộc trò chuyện đã kết thúc.");
        conversation.Close();
        await _unitOfWork.SaveChangesAsync();
        await _notifier.ChatMessageAsync(conversation.Id, ChatMapping.ToDto(message, new Dictionary<long, string>()));
        await NotifyChangedAsync(conversation);
    }

    public async Task<bool> CanJoinAsync(int conversationId, string? guestKey, long? userId, bool isManagement)
    {
        var c = await _conversationRepository.GetAll().AsNoTracking()
            .Where(x => x.Id == conversationId)
            .Select(x => new { x.UserId, x.GuestKey })
            .FirstOrDefaultAsync();
        if (c is null)
            return false;
        return isManagement || (c.UserId is { } owner ? owner == userId : KeyMatches(c.GuestKey, guestKey));
    }

    // ------------------------------------------------------------------ Hỗ trợ

    /// <summary>
    /// Hội thoại của người đang gọi: theo mã (phải đúng chủ — tài khoản hoặc khóa khách), hoặc khách đã đăng nhập không
    /// gửi mã thì lấy hội thoại đang mở gần nhất. Hội thoại của người khác được coi như không tồn tại.
    /// </summary>
    private async Task<ChatConversation?> FindAsync(ChatAccessInput input)
    {
        var userId = _session.UserId;
        if (input.ConversationId is { } id)
        {
            var c = await _conversationRepository.GetAll().FirstOrDefaultAsync(x => x.Id == id);
            if (c is null)
                return null;
            var owned = c.UserId is { } owner ? owner == userId : KeyMatches(c.GuestKey, input.GuestKey);
            return owned ? c : null;
        }
        if (userId is null)
            return null;
        return await _conversationRepository.GetAll()
            .Where(x => x.UserId == userId && x.Status != ChatConversationStatus.Closed)
            .OrderByDescending(x => x.LastMessageTime)
            .FirstOrDefaultAsync();
    }

    /// <summary>Hội thoại đang mở; chưa có hoặc đã kết thúc thì tạo mới. Trả kèm khóa khách (khách vãng lai).</summary>
    private async Task<(ChatConversation, string?)> FindOrCreateAsync(ChatAccessInput input)
    {
        var conversation = await FindAsync(input);
        if (conversation is { IsClosed: false })
            return (conversation, conversation.UserId is null ? input.GuestKey : null);

        var userId = _session.UserId;
        var guestKey = userId is null ? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant() : null;
        conversation = new ChatConversation(userId, guestKey);
        await _conversationRepository.InsertAsync(conversation);
        return (conversation, guestKey);
    }

    private async Task AddSystemMessageAsync(ChatConversation conversation, string text)
    {
        var message = conversation.AddMessage(ChatSender.System, text);
        await _unitOfWork.SaveChangesAsync();
        await _notifier.ChatMessageAsync(conversation.Id, ChatMapping.ToDto(message, new Dictionary<long, string>()));
        await NotifyChangedAsync(conversation);
    }

    private Task NotifyChangedAsync(ChatConversation c) =>
        _notifier.ChatConversationChangedAsync(new ChatConversationChangedEvent(c.Id, c.Status, c.StaffUnread, c.StaffUserId));

    private async Task<ChatConversationDto> ToDtoAsync(ChatConversation c, string? guestKey)
    {
        var messages = await _messageRepository.GetAll().AsNoTracking()
            .Where(m => m.ConversationId == c.Id)
            .OrderByDescending(m => m.Id)
            .Take(HistoryLimit)
            .ToListAsync();
        messages.Reverse();
        var names = await ChatMapping.NamesAsync(_userRepository, messages.Select(m => m.StaffUserId).Append(c.StaffUserId));
        return new ChatConversationDto
        {
            Id = c.Id,
            Status = c.Status,
            GuestKey = c.UserId is null ? guestKey : null,
            StaffOnline = _presence.AnyStaffOnline(),
            StaffName = c.StaffUserId is { } sid && names.TryGetValue(sid, out var name) ? name : null,
            Messages = messages.Select(m => ChatMapping.ToDto(m, names)).ToList(),
            CreationTime = c.CreationTime
        };
    }

    /// <summary>So khóa khách theo thời gian cố định (không lộ độ dài phần khớp qua thời gian phản hồi).</summary>
    private static bool KeyMatches(string? expected, string? actual) =>
        expected is not null && actual is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
