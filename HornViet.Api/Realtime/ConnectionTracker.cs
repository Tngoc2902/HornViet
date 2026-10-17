using System.Collections.Concurrent;

namespace HornViet.Api.Realtime;

/// <summary>
/// Ghi nhớ kết nối SignalR nào thuộc tài khoản nào (singleton, trong bộ nhớ của tiến trình).
/// Dùng khi thu hồi phiên: gỡ các kết nối của tài khoản khỏi nhóm riêng và nhóm quản trị ngay lập tức,
/// kể cả khi giao diện bỏ qua sự kiện SessionRevoked.
/// </summary>
/// <remarks>Chạy nhiều máy chủ (scale-out) thì cần backplane (VD Redis) thay cho lớp này.</remarks>
public class ConnectionTracker
{
    public record Connection(string ConnectionId, long UserId, bool IsManagement);

    private readonly ConcurrentDictionary<string, Connection> _connections = new();

    public void Add(string connectionId, long userId, bool isManagement) =>
        _connections[connectionId] = new Connection(connectionId, userId, isManagement);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyList<Connection> ForUser(long userId) =>
        _connections.Values.Where(c => c.UserId == userId).ToList();
}
