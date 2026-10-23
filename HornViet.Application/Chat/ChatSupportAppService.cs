using HornViet.Application.Common;
using HornViet.Domain.Chat.Entities;
using HornViet.Domain.Chat.Enums;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Chat;

/// <summary>Trang quản trị "Hỗ trợ khách hàng": nhân viên xem hội thoại, tiếp nhận và trả lời khách trực tiếp.</summary>
public interface IChatSupportAppService
{
    Task<PagedResultDto<ChatConversationListItemDto>> GetConversationsAsync(GetChatConversationsInput input);

    /// <summary>Chi tiết hội thoại; nhân viên phụ trách (hoặc hội thoại đang chờ) mở xem thì đánh dấu đã đọc.</summary>
    Task<ChatSupportConversationDto> GetConversationAsync(int id);

    Task<ChatSupportConversationDto> ClaimAsync(ClaimChatInput input);

    /// <summary>Trả lời khách; hội thoại chưa ai phụ trách thì tự tiếp nhận.</summary>
    Task<ChatMessageDto> ReplyAsync(StaffReplyInput input);

    Task<ChatSupportConversationDto> CloseAsync(int id);

    /// <summary>Số hội thoại đang chờ và số tin chưa đọc của nhân viên đang đăng nhập (huy hiệu ở menu).</summary>
    Task<ChatSupportSummaryDto> GetSummaryAsync();
}

public class ChatSupportAppService : IChatSupportAppService
{
    private const int HistoryLimit = 200;
    private const int PreviewLength = 120;

    private readonly IRepository<ChatConversation, int> _conversationRepository;
    private readonly IRepository<ChatMessage, long> _messageRepository;
    private readonly IRepository<User, long> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;
    private readonly IRealtimeNotifier _notifier;
    private readonly IStaffPresence _presence;

    public ChatSupportAppService(IRepository<ChatConversation, int> conversationRepository,
        IRepository<ChatMessage, long> messageRepository, IRepository<User, long> userRepository, IUnitOfWork unitOfWork,
        IAppSession session, IRealtimeNotifier notifier, IStaffPresence presence)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _session = session;
        _notifier = notifier;
        _presence = presence;
    }

    public async Task<PagedResultDto<ChatConversationListItemDto>> GetConversationsAsync(GetChatConversationsInput input)
    {
        var query = _conversationRepository.GetAll().AsNoTracking();
        query = input.Status is { } status
            ? query.Where(c => c.Status == status)
            : query.Where(c => c.Status == ChatConversationStatus.WaitingStaff || c.Status == ChatConversationStatus.WithStaff);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Status == ChatConversationStatus.WaitingStaff ? 0 : 1)
            .ThenByDescending(c => c.LastMessageTime)
            .PageBy(input)
            .Select(c => new ChatConversationListItemDto
            {
                Id = c.Id,
                Status = c.Status,
                CustomerName = _userRepository.GetAll().Where(u => u.Id == c.UserId).Select(u => u.Name).FirstOrDefault() ?? "Khách vãng lai",
                IsGuest = c.UserId == null,
                StaffUserId = c.StaffUserId,
                StaffName = _userRepository.GetAll().Where(u => u.Id == c.StaffUserId).Select(u => u.Name).FirstOrDefault(),
                LastMessage = c.Messages.OrderByDescending(m => m.Id).Select(m => m.Content).FirstOrDefault(),
                LastSender = c.Messages.OrderByDescending(m => m.Id).Select(m => (ChatSender?)m.Sender).FirstOrDefault(),
                StaffUnread = c.StaffUnread,
                MessageCount = c.Messages.Count,
                LastMessageTime = c.LastMessageTime,
                CreationTime = c.CreationTime
            })
            .ToListAsync();
        foreach (var item in items.Where(i => i.LastMessage is { Length: > PreviewLength }))
            item.LastMessage = item.LastMessage![..PreviewLength] + "…";
        return new PagedResultDto<ChatConversationListItemDto>(total, items);
    }

    public async Task<ChatSupportConversationDto> GetConversationAsync(int id)
    {
        var conversation = await LoadAsync(id);
        var me = _session.GetUserId();
        if (conversation.StaffUnread > 0 &&
            (conversation.StaffUserId == me || conversation.Status == ChatConversationStatus.WaitingStaff))
        {
            conversation.MarkReadByStaff();
            await _unitOfWork.SaveChangesAsync();
            await NotifyChangedAsync(conversation);
        }
        return await ToDtoAsync(conversation);
    }

    public async Task<ChatSupportConversationDto> ClaimAsync(ClaimChatInput input)
    {
        var conversation = await LoadAsync(input.ConversationId);
        await ClaimCoreAsync(conversation, input.TakeOver);
        return await ToDtoAsync(conversation);
    }

    public async Task<ChatMessageDto> ReplyAsync(StaffReplyInput input)
    {
        var text = input.Text.Trim();
        if (text.Length == 0)
            throw new UserFriendlyException("Vui lòng nhập tin nhắn.");
        var conversation = await LoadAsync(input.ConversationId);
        var me = _session.GetUserId();
        if (conversation.Status != ChatConversationStatus.WithStaff || conversation.StaffUserId != me)
            await ClaimCoreAsync(conversation, takeOver: false);

        var message = conversation.AddMessage(ChatSender.Staff, text, me);
        conversation.MarkReadByStaff();
        await _unitOfWork.SaveChangesAsync();

        var names = await ChatMapping.NamesAsync(_userRepository, [me]);
        var dto = ChatMapping.ToDto(message, names);
        await _notifier.ChatMessageAsync(conversation.Id, dto);
        await NotifyChangedAsync(conversation);
        return dto;
    }

    public async Task<ChatSupportConversationDto> CloseAsync(int id)
    {
        var conversation = await LoadAsync(id);
        if (!conversation.IsClosed)
        {
            var message = conversation.AddMessage(ChatSender.System,
                "Nhân viên đã kết thúc cuộc trò chuyện. Cảm ơn bạn đã liên hệ Hồn Sừng Việt!");
            conversation.Close();
            await _unitOfWork.SaveChangesAsync();
            await _notifier.ChatMessageAsync(conversation.Id, ChatMapping.ToDto(message, new Dictionary<long, string>()));
            await NotifyChangedAsync(conversation);
        }
        return await ToDtoAsync(conversation);
    }

    public async Task<ChatSupportSummaryDto> GetSummaryAsync()
    {
        var me = _session.GetUserId();
        var open = _conversationRepository.GetAll().AsNoTracking();
        return new ChatSupportSummaryDto
        {
            WaitingCount = await open.CountAsync(c => c.Status == ChatConversationStatus.WaitingStaff),
            MineUnread = await open.Where(c => c.Status == ChatConversationStatus.WithStaff && c.StaffUserId == me)
                .SumAsync(c => (int?)c.StaffUnread) ?? 0
        };
    }

    // ------------------------------------------------------------------ Hỗ trợ

    private async Task<ChatConversation> LoadAsync(int id) =>
        await _conversationRepository.GetAll().FirstOrDefaultAsync(c => c.Id == id)
        ?? throw new EntityNotFoundException("Không tìm thấy cuộc trò chuyện.");

    private async Task ClaimCoreAsync(ChatConversation conversation, bool takeOver)
    {
        var me = _session.GetUserId();
        if (conversation.Status == ChatConversationStatus.WithStaff && conversation.StaffUserId == me)
            return;
        conversation.AssignStaff(me, takeOver);
        conversation.MarkReadByStaff();
        var names = await ChatMapping.NamesAsync(_userRepository, [me]);
        var staffName = names.GetValueOrDefault(me, "Nhân viên");
        var message = conversation.AddMessage(ChatSender.System, $"{staffName} đã tham gia cuộc trò chuyện.");
        await _unitOfWork.SaveChangesAsync();
        await _notifier.ChatMessageAsync(conversation.Id, ChatMapping.ToDto(message, names));
        await NotifyChangedAsync(conversation);
    }

    private Task NotifyChangedAsync(ChatConversation c) =>
        _notifier.ChatConversationChangedAsync(new ChatConversationChangedEvent(c.Id, c.Status, c.StaffUnread, c.StaffUserId));

    private async Task<ChatSupportConversationDto> ToDtoAsync(ChatConversation c)
    {
        var messages = await _messageRepository.GetAll().AsNoTracking()
            .Where(m => m.ConversationId == c.Id)
            .OrderByDescending(m => m.Id)
            .Take(HistoryLimit)
            .ToListAsync();
        messages.Reverse();
        var names = await ChatMapping.NamesAsync(_userRepository,
            messages.Select(m => m.StaffUserId).Append(c.StaffUserId).Append(c.UserId));
        return new ChatSupportConversationDto
        {
            Id = c.Id,
            Status = c.Status,
            StaffOnline = _presence.AnyStaffOnline(),
            StaffUserId = c.StaffUserId,
            StaffName = c.StaffUserId is { } sid ? names.GetValueOrDefault(sid) : null,
            CustomerName = c.UserId is { } uid ? names.GetValueOrDefault(uid, "Khách hàng") : "Khách vãng lai",
            IsGuest = c.UserId is null,
            IsMine = c.StaffUserId == _session.UserId,
            Messages = messages.Select(m => ChatMapping.ToDto(m, names)).ToList(),
            CreationTime = c.CreationTime
        };
    }
}
