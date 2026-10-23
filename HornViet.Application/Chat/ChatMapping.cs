using System.Text.Json;
using HornViet.Domain.Chat.Entities;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Chat;

/// <summary>Đổi tin nhắn (entity) sang DTO và ghi / đọc phần dữ liệu kèm theo (Payload JSON) của tin bot.</summary>
internal static class ChatMapping
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string? SerializePayload(ChatPayload p) =>
        p.Products is null && p.Orders is null && p.Links is null && p.QuickReplies is null && !p.SuggestStaff && !p.Ai
            ? null
            : JsonSerializer.Serialize(p, Json);

    public static ChatMessageDto ToDto(ChatMessage m, IReadOnlyDictionary<long, string> staffNames)
    {
        var payload = string.IsNullOrEmpty(m.Payload) ? null : JsonSerializer.Deserialize<ChatPayload>(m.Payload, Json);
        return new ChatMessageDto
        {
            Id = m.Id,
            Sender = m.Sender,
            Content = m.Content,
            StaffName = m.StaffUserId is { } sid && staffNames.TryGetValue(sid, out var name) ? name : null,
            CreationTime = m.CreationTime,
            Products = payload?.Products,
            Orders = payload?.Orders,
            Links = payload?.Links,
            QuickReplies = payload?.QuickReplies,
            SuggestStaff = payload?.SuggestStaff ?? false,
            Ai = payload?.Ai ?? false
        };
    }

    public static async Task<Dictionary<long, string>> NamesAsync(IRepository<User, long> users, IEnumerable<long?> ids)
    {
        var wanted = ids.OfType<long>().Distinct().ToList();
        if (wanted.Count == 0)
            return new Dictionary<long, string>();
        return await users.GetAll().AsNoTracking().Where(u => wanted.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
    }
}
