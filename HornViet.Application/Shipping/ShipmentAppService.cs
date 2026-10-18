using HornViet.Application.Common;
using HornViet.Application.Orders;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.Orders.Entities;
using HornViet.Domain.Orders.Enums;
using HornViet.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HornViet.Application.Shipping;

/// <summary>Tiến trình giao hàng của một đơn — dùng cho cả khách (xem đơn của mình) và nhân viên.</summary>
public class ShipmentDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = null!;

    /// <summary>Mã vận đơn của đơn vị vận chuyển; null = chưa tạo vận đơn.</summary>
    public string? TrackingCode { get; set; }

    /// <summary>Mã trạng thái giao hàng (ready_to_pick, delivering, delivered…); frontend dịch sang tiếng Việt.</summary>
    public string? Status { get; set; }

    public DateTime? ExpectedDeliveryTime { get; set; }
    public DateTime? StatusTime { get; set; }
    public decimal ShippingFee { get; set; }
}

/// <summary>Dữ liệu GHN gọi ngược về khi trạng thái đơn đổi (khóa dạng PascalCase theo tài liệu GHN).</summary>
public class GhnCallbackInput
{
    public string? Type { get; set; }
    public string? OrderCode { get; set; }
    public string? ClientOrderCode { get; set; }
    public string? Status { get; set; }
    public DateTime? Time { get; set; }
    public string? Reason { get; set; }
}

public class SimulateShipmentInput
{
    public int OrderId { get; set; }

    /// <summary>Trạng thái GHN muốn giả lập: picked, delivering, delivered, delivery_fail, returned…</summary>
    public string Status { get; set; } = null!;
}

public interface IShipmentAppService
{
    /// <summary>
    /// Chỉ môi trường thử (sandbox không có shipper nên vận đơn đứng mãi ở "Chờ lấy hàng"): áp trạng thái giao hàng như thể
    /// GHN vừa gửi webhook, để thử trọn luồng đã lấy hàng → đang giao → đã giao / hoàn hàng.
    /// </summary>
    Task<ShipmentDto> SimulateAsync(SimulateShipmentInput input);

    /// <summary>Tạo vận đơn cho đơn hàng (nhân viên). Đơn "Chờ xử lý" tự chuyển sang "Đang giao".</summary>
    Task<ShipmentDto> CreateAsync(int orderId);

    /// <summary>
    /// Tự tạo vận đơn ngay sau khi khách đặt hàng. Không bao giờ ném lỗi: thất bại (GHN lỗi, thiếu kho…) chỉ ghi log, đơn
    /// vẫn "Chờ xử lý" để nhân viên tạo vận đơn thủ công. Đơn giữ "Chờ xử lý" đến khi đơn vị vận chuyển lấy hàng.
    /// </summary>
    Task TryAutoCreateAsync(int orderId);

    /// <summary>Hủy vận đơn trên đơn vị vận chuyển để tạo lại (đơn chưa được lấy hàng).</summary>
    Task<ShipmentDto> CancelAsync(int orderId);

    /// <summary>Đường dẫn mở phiếu gửi hàng để in.</summary>
    Task<string> GetLabelUrlAsync(int orderId);

    /// <summary>Tiến trình giao hàng; làm mới từ đơn vị vận chuyển nếu <paramref name="refresh"/> (chủ đơn hoặc nhân viên).</summary>
    Task<ShipmentDto> GetAsync(int orderId, bool refresh);

    /// <summary>Xử lý webhook của đơn vị vận chuyển. Luôn kết thúc êm (kể cả đơn lạ) để họ không gửi lại mãi.</summary>
    Task HandleCallbackAsync(GhnCallbackInput input);
}

public class ShipmentAppService : IShipmentAppService
{
    private readonly IRepository<Order, int> _orderRepository;
    private readonly IShippingProvider _provider;
    private readonly IShippingAppService _shipping;
    private readonly IOrderAppService _orderService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<ShipmentAppService> _logger;

    public ShipmentAppService(IRepository<Order, int> orderRepository, IShippingProvider provider, IShippingAppService shipping, IOrderAppService orderService,
        IUnitOfWork unitOfWork, IAppSession session, IRealtimeNotifier notifier, ILogger<ShipmentAppService> logger)
    {
        _orderRepository = orderRepository;
        _provider = provider;
        _shipping = shipping;
        _orderService = orderService;
        _unitOfWork = unitOfWork;
        _session = session;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<ShipmentDto> CreateAsync(int orderId)
    {
        EnsureEnabled();
        var order = await LoadAsync(orderId);
        await CreateCoreAsync(order, moveToShipping: true);
        return ToDto(order);
    }

    public async Task TryAutoCreateAsync(int orderId)
    {
        try
        {
            if (!_provider.IsEnabled)
                return;
            var order = await _orderRepository.GetAll().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.Status != OrderStatus.Pending || order.ToProvinceId is null)
                return;
            await CreateCoreAsync(order, moveToShipping: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tự tạo vận đơn cho đơn {OrderId} thất bại, nhân viên cần tạo thủ công.", orderId);
        }
    }

    private async Task CreateCoreAsync(Order order, bool moveToShipping)
    {
        if (!string.IsNullOrEmpty(order.GhnOrderCode))
            throw new UserFriendlyException($"Đơn {Order.FormatCode(order.Id)} đã có vận đơn {order.GhnOrderCode}.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Shipping))
            throw new UserFriendlyException("Chỉ tạo vận đơn cho đơn đang chờ xử lý hoặc đang giao.");
        if (order.ToProvinceId is null || string.IsNullOrEmpty(order.ToWardCode))
            throw new UserFriendlyException(order.ToDistrictId != null
                ? "Đơn này dùng địa chỉ cũ (có quận / huyện) mà GHN không còn nhận. Vui lòng tạo vận đơn thủ công trên cổng GHN."
                : "Đơn này đặt khi chưa bật giao hàng nhanh nên không có mã tỉnh / phường để tạo vận đơn.");
        var (provinceName, wardName) = await _shipping.ResolveNamesAsync(order.ToProvinceId.Value, order.ToWardCode);

        var code = Order.FormatCode(order.Id);
        // Mã đơn gửi GHN phải duy nhất; tạo lại sau khi hủy mà gửi lại mã cũ thì GHN trả về vận đơn đã hủy → thêm hậu tố thời gian.
        var clientCode = order.GhnStatusTime != null ? $"{code}-{DateTime.Now:MMddHHmmss}" : code;
        var weight = order.Items.Sum(i => i.Quantity) * _provider.DefaultItemWeightGrams;

        var created = await _provider.CreateShipmentAsync(new ShipmentRequest(
            clientCode, order.ReceiverName, order.PhoneNumber, order.ToStreet ?? order.ShippingAddress, provinceName, wardName,
            // Thu hộ đúng số khách phải trả khi COD; chuyển khoản (đã thanh toán) thì không thu hộ.
            order.PaymentMethod == PaymentMethod.COD ? order.PayableAmount : 0,
            order.TotalAmount, weight, $"Đơn {code} – Hồn Sừng Việt", order.Note,
            order.Items.Select(i => new ShipmentItem($"{i.ProductName} ({i.VariantName})", i.Quantity, i.UnitPrice,
                _provider.DefaultItemWeightGrams)).ToList()));

        order.AttachShipment(created.OrderCode, created.ExpectedDeliveryTime);
        if (moveToShipping)
            await _orderService.ApplyShipmentStatusAsync(order.Id, OrderStatus.Shipping); // Chờ xử lý → Đang giao, lưu cùng vận đơn
        else
            await _unitOfWork.SaveChangesAsync();
        await NotifyAsync(order);
    }

    public async Task<ShipmentDto> CancelAsync(int orderId)
    {
        EnsureEnabled();
        var order = await LoadAsync(orderId);
        if (string.IsNullOrEmpty(order.GhnOrderCode))
            throw new UserFriendlyException("Đơn này chưa có vận đơn.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Shipping))
            throw new UserFriendlyException("Đơn đã hoàn thành hoặc đã hủy, không hủy vận đơn được.");

        await _provider.CancelShipmentAsync(order.GhnOrderCode, $"Hủy vận đơn đơn {Order.FormatCode(order.Id)}");
        order.DetachShipment();
        await _unitOfWork.SaveChangesAsync();
        await NotifyAsync(order);
        return ToDto(order);
    }

    public async Task<string> GetLabelUrlAsync(int orderId)
    {
        EnsureEnabled();
        var order = await LoadAsync(orderId);
        if (string.IsNullOrEmpty(order.GhnOrderCode))
            throw new UserFriendlyException("Đơn này chưa có vận đơn để in.");
        return await _provider.GetPrintUrlAsync(order.GhnOrderCode);
    }

    public async Task<ShipmentDto> GetAsync(int orderId, bool refresh)
    {
        var order = await LoadAsync(orderId, ownerAllowed: true);
        if (refresh && _provider.IsEnabled && !string.IsNullOrEmpty(order.GhnOrderCode) && !IsFinal(order.GhnStatus))
        {
            try
            {
                var info = await _provider.GetShipmentAsync(order.GhnOrderCode);
                await ApplyStatusAsync(order, info.Status, info.UpdatedTime);
            }
            catch (UserFriendlyException ex)
            {
                // Làm mới thủ công thất bại không phải lỗi nghiêm trọng: vẫn trả tình trạng đang lưu.
                _logger.LogWarning("Không làm mới được vận đơn {Code}: {Message}", order.GhnOrderCode, ex.Message);
            }
        }
        return ToDto(order);
    }

    private static readonly HashSet<string> SimulatableStatuses =
        ["picking", "picked", "storing", "transporting", "delivering", "delivered", "delivery_fail", "waiting_to_return", "returning", "returned"];

    public async Task<ShipmentDto> SimulateAsync(SimulateShipmentInput input)
    {
        EnsureEnabled();
        if (!_provider.IsSandbox)
            throw new UserFriendlyException("Chỉ giả lập được trạng thái giao hàng ở môi trường thử.");
        var status = input.Status?.Trim().ToLowerInvariant() ?? "";
        if (!SimulatableStatuses.Contains(status))
            throw new UserFriendlyException("Trạng thái giả lập không hợp lệ.");

        var order = await LoadAsync(input.OrderId);
        if (string.IsNullOrEmpty(order.GhnOrderCode))
            throw new UserFriendlyException("Đơn này chưa có vận đơn.");
        if (IsFinal(order.GhnStatus))
            throw new UserFriendlyException("Vận đơn đã kết thúc (đã giao / đã hoàn), không đổi được nữa.");

        await ApplyStatusAsync(order, status, DateTime.Now);
        return ToDto(order);
    }

    public async Task HandleCallbackAsync(GhnCallbackInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Status) || (input.Type != null && !input.Type.Equals("switch_status", StringComparison.OrdinalIgnoreCase)))
            return; // chỉ quan tâm sự kiện đổi trạng thái

        var order = await _orderRepository.GetAll().Include(o => o.Items).FirstOrDefaultAsync(o =>
            o.GhnOrderCode == input.OrderCode && input.OrderCode != null);
        if (order == null)
        {
            _logger.LogInformation("Webhook GHN cho vận đơn {Code} không khớp đơn nào (đã hủy / đơn lạ).", input.OrderCode);
            return;
        }

        // GHN có thể gửi lặp và không đảm bảo thứ tự: bỏ qua sự kiện cũ hơn trạng thái đang lưu.
        if (input.Time is { } t && order.GhnStatusTime is { } stored && t.ToLocalTime() < stored)
            return;

        await ApplyStatusAsync(order, input.Status!.Trim().ToLowerInvariant(), input.Time?.ToLocalTime());
    }

    // ------------------------------------------------------------------ Hỗ trợ

    private async Task ApplyStatusAsync(Order order, string ghnStatus, DateTime? time)
    {
        if (IsFinal(order.GhnStatus))
            return; // đã giao / đã hoàn: không nhận thêm cập nhật

        var changed = order.UpdateShipmentStatus(ghnStatus, time);
        if (!changed)
            return;

        try
        {
            // Giao thành công → Hoàn thành; hàng hoàn về shop → Hủy và hoàn tồn kho. Trạng thái khác chỉ ghi nhận tiến trình.
            if (ghnStatus == "delivered")
                await _orderService.ApplyShipmentStatusAsync(order.Id, OrderStatus.Completed);
            else if (ghnStatus == "returned")
                await _orderService.ApplyShipmentStatusAsync(order.Id, OrderStatus.Canceled);
            else if (order.Status == OrderStatus.Pending && PickedUp.Contains(ghnStatus))
                await _orderService.ApplyShipmentStatusAsync(order.Id, OrderStatus.Shipping); // GHN đã lấy hàng → Đang giao
            else
                await _unitOfWork.SaveChangesAsync();
        }
        catch (UserFriendlyException ex)
        {
            // VD đơn đã được nhân viên chuyển Hoàn thành / Hủy bằng tay: vẫn lưu trạng thái giao hàng.
            _logger.LogWarning("Không đổi được trạng thái đơn {OrderId} theo GHN {Status}: {Message}", order.Id, ghnStatus, ex.Message);
            await _unitOfWork.SaveChangesAsync();
        }

        await NotifyAsync(order);
    }

    private Task NotifyAsync(Order order) =>
        _notifier.ShipmentChangedAsync(new ShipmentChangedEvent(order.Id, Order.FormatCode(order.Id), order.UserId, order.GhnStatus));

    private static readonly HashSet<string> PickedUp = ["picked", "storing", "sorting", "transporting", "delivering"];

    private static bool IsFinal(string? status) => status is "delivered" or "returned";

    private void EnsureEnabled()
    {
        if (!_provider.IsEnabled)
            throw new UserFriendlyException("Cửa hàng chưa bật dịch vụ giao hàng.");
    }

    /// <summary>Nhân viên xem được mọi đơn; khách chỉ xem đơn của mình.</summary>
    private async Task<Order> LoadAsync(int orderId, bool ownerAllowed = false)
    {
        var order = await _orderRepository.GetAll().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId)
                    ?? throw new EntityNotFoundException("Không tìm thấy đơn hàng.");
        if (_session.IsManagement())
            return order;
        if (ownerAllowed && order.UserId == _session.GetUserId())
            return order;
        throw new EntityNotFoundException("Không tìm thấy đơn hàng.");
    }

    private static ShipmentDto ToDto(Order o) => new()
    {
        OrderId = o.Id,
        OrderCode = Order.FormatCode(o.Id),
        TrackingCode = o.GhnOrderCode,
        Status = o.GhnStatus,
        ExpectedDeliveryTime = o.GhnExpectedDeliveryTime,
        StatusTime = o.GhnStatusTime,
        ShippingFee = o.ShippingFee
    };
}
