using System.Security.Claims;
using HornViet.Domain.Users.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HornViet.Api.Realtime;

/// <summary>
/// Các sự kiện server đẩy xuống giao diện (tên phương thức = tên sự kiện phía client, VD connection.on("OrderCreated")).
/// Hub kiểu mạnh: gọi nhầm tên sự kiện sẽ lỗi lúc biên dịch.
/// </summary>
public interface INotificationClient
{
    Task OrderCreated(OrderCreatedMessage message);
    Task OrderStatusChanged(OrderStatusChangedMessage message);
    Task StockChanged(StockChangedMessage message);
    Task LowStock(LowStockMessage message);
    Task ProductChanged(ProductChangedMessage message);
    Task SessionRevoked(SessionRevokedMessage message);
}

public record OrderCreatedMessage(int OrderId, string Code, int ItemCount, decimal TotalAmount, DateTime CreationTime);
public record OrderStatusChangedMessage(int OrderId, string Code, int Status);
public record VariantStock(int VariantId, int StockQuantity);
public record StockChangedMessage(int ProductId, IReadOnlyList<VariantStock> Variants);
public record LowStockMessage(int ProductId, string ProductName, int VariantId, string VariantName, int StockQuantity);
public record ProductChangedMessage(int ProductId);
/// <summary>Reason: locked | rolesChanged | passwordReset | passwordChanged.</summary>
public record SessionRevokedMessage(string Reason);

/// <summary>
/// Kênh thời gian thực (WebSocket qua SignalR) tại <see cref="Path"/>. Khách vãng lai kết nối được để theo dõi tồn kho
/// sản phẩm đang xem; người đã đăng nhập (JWT gửi qua ?access_token=) được thêm vào nhóm riêng theo tài khoản và vai trò.
/// </summary>
/// <remarks>
/// Nhóm: <c>user:{id}</c> (thông báo riêng của một tài khoản), <c>management</c> (Nhân viên + Quản trị viên),
/// <c>product:{id}</c> (người đang mở trang chi tiết sản phẩm). Token không hợp lệ / đã bị thu hồi (SecurityStamp đổi)
/// bị JwtBearer từ chối nên kết nối chỉ còn quyền như khách vãng lai.
/// </remarks>
[AllowAnonymous]
public class NotificationHub : Hub<INotificationClient>
{
    public const string Path = "/hubs/notifications";
    public const string ManagementGroup = "management";
    public static string UserGroup(long userId) => $"user:{userId}";
    public static string ProductGroup(int productId) => $"product:{productId}";

    private readonly ConnectionTracker _tracker;

    public NotificationHub(ConnectionTracker tracker) => _tracker = tracker;

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true &&
            long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var isManagement = user.IsInRole(nameof(RoleName.Admin)) || user.IsInRole(nameof(RoleName.Staff));
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            if (isManagement)
                await Groups.AddToGroupAsync(Context.ConnectionId, ManagementGroup);
            _tracker.Add(Context.ConnectionId, userId, isManagement);
        }
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client gọi khi mở trang chi tiết sản phẩm để nhận tồn kho mới.</summary>
    public Task JoinProduct(int productId) =>
        productId > 0 ? Groups.AddToGroupAsync(Context.ConnectionId, ProductGroup(productId)) : Task.CompletedTask;

    /// <summary>Client gọi khi rời trang chi tiết sản phẩm.</summary>
    public Task LeaveProduct(int productId) =>
        productId > 0 ? Groups.RemoveFromGroupAsync(Context.ConnectionId, ProductGroup(productId)) : Task.CompletedTask;
}
