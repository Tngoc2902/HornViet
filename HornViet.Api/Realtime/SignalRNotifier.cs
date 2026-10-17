using HornViet.Application.Common;
using HornViet.Domain;
using Microsoft.AspNetCore.SignalR;

namespace HornViet.Api.Realtime;

/// <summary>
/// Cài đặt <see cref="IRealtimeNotifier"/> của tầng Application bằng SignalR. Mọi lỗi khi gửi chỉ được ghi log:
/// thao tác nghiệp vụ đã lưu xong, không được thất bại vì không gửi được thông báo.
/// </summary>
public class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hub;
    private readonly ConnectionTracker _tracker;
    private readonly ILogger<SignalRNotifier> _logger;

    public SignalRNotifier(IHubContext<NotificationHub, INotificationClient> hub, ConnectionTracker tracker,
        ILogger<SignalRNotifier> logger)
    {
        _hub = hub;
        _tracker = tracker;
        _logger = logger;
    }

    public Task OrderCreatedAsync(OrderCreatedEvent e) => SafeAsync(nameof(OrderCreatedAsync), () =>
        _hub.Clients.Group(NotificationHub.ManagementGroup)
            .OrderCreated(new OrderCreatedMessage(e.OrderId, e.Code, e.ItemCount, e.TotalAmount, e.CreationTime)));

    public Task OrderStatusChangedAsync(OrderStatusChangedEvent e) => SafeAsync(nameof(OrderStatusChangedAsync), () =>
        // Chủ đơn và các nhân viên khác đang mở trang quản trị cùng được cập nhật.
        _hub.Clients.Groups(NotificationHub.UserGroup(e.UserId), NotificationHub.ManagementGroup)
            .OrderStatusChanged(new OrderStatusChangedMessage(e.OrderId, e.Code, (int)e.Status)));

    public Task StockChangedAsync(IReadOnlyCollection<StockChange> changes, bool alertLowStock = false) =>
        SafeAsync(nameof(StockChangedAsync), async () =>
        {
            foreach (var product in changes.GroupBy(c => c.ProductId))
            {
                var message = new StockChangedMessage(product.Key,
                    product.Select(c => new VariantStock(c.VariantId, c.StockQuantity)).ToList());
                await _hub.Clients.Groups(NotificationHub.ProductGroup(product.Key), NotificationHub.ManagementGroup)
                    .StockChanged(message);
            }

            if (!alertLowStock)
                return;
            foreach (var c in changes.Where(c => c.StockQuantity <= AppConsts.LowStockThreshold))
                await _hub.Clients.Group(NotificationHub.ManagementGroup)
                    .LowStock(new LowStockMessage(c.ProductId, c.ProductName, c.VariantId, c.VariantName, c.StockQuantity));
        });

    public Task ProductChangedAsync(int productId) => SafeAsync(nameof(ProductChangedAsync), () =>
        _hub.Clients.Groups(NotificationHub.ProductGroup(productId), NotificationHub.ManagementGroup)
            .ProductChanged(new ProductChangedMessage(productId)));

    public Task SessionRevokedAsync(long userId, SessionRevokedReason reason) =>
        SafeAsync(nameof(SessionRevokedAsync), async () =>
        {
            var reasonText = char.ToLowerInvariant(reason.ToString()[0]) + reason.ToString()[1..];
            await _hub.Clients.Group(NotificationHub.UserGroup(userId)).SessionRevoked(new SessionRevokedMessage(reasonText));

            // Không tin vào việc giao diện tự đăng xuất: gỡ ngay kết nối của tài khoản khỏi các nhóm riêng tư.
            foreach (var c in _tracker.ForUser(userId))
            {
                await _hub.Groups.RemoveFromGroupAsync(c.ConnectionId, NotificationHub.UserGroup(userId));
                if (c.IsManagement)
                    await _hub.Groups.RemoveFromGroupAsync(c.ConnectionId, NotificationHub.ManagementGroup);
                _tracker.Remove(c.ConnectionId);
            }
        });

    private async Task SafeAsync(string operation, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không gửi được thông báo thời gian thực ({Operation}).", operation);
        }
    }
}
