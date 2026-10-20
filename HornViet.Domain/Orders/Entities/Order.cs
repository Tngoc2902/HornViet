using HornViet.Domain.SharedKernel;
using HornViet.Domain.Orders.Enums;

namespace HornViet.Domain.Orders.Entities;

/// <summary>Aggregate root đơn hàng: giữ máy trạng thái và tổng tiền.</summary>
public class Order : Entity<int>, IHasCreationTime
{
    public long UserId { get; private set; }
    public string ReceiverName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string ShippingAddress { get; private set; } = null!;
    public decimal TotalAmount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Giao tận nơi hay nhận tại cửa hàng — quyết định luồng trạng thái (xem ChangeStatus).</summary>
    public DeliveryMethod DeliveryMethod { get; private set; }

    public string? Note { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastStatusChangeTime { get; private set; }

    // ---- Giao hàng (Giao Hàng Nhanh)

    /// <summary>Phí vận chuyển khách trả (đồng). TotalAmount vẫn là tiền hàng nên thống kê doanh thu / lợi nhuận không đổi.</summary>
    public decimal ShippingFee { get; private set; }

    /// <summary>Mã tỉnh / thành và phường / xã (danh mục mới 2 cấp của GHN) của địa chỉ nhận; null khi đơn tạo lúc chưa bật vận chuyển.</summary>
    public int? ToProvinceId { get; private set; }
    public string? ToWardCode { get; private set; }

    /// <summary>Số nhà, tên đường khách nhập (ShippingAddress là bản đầy đủ đã ghép phường / tỉnh).</summary>
    public string? ToStreet { get; private set; }

    /// <summary>Mã quận / huyện theo địa chỉ cũ 3 cấp — chỉ còn ở đơn đặt trước khi chuyển sang địa chỉ mới.</summary>
    public int? ToDistrictId { get; private set; }

    /// <summary>Mã vận đơn GHN (order_code); null = chưa tạo vận đơn.</summary>
    public string? GhnOrderCode { get; private set; }

    /// <summary>Trạng thái giao hàng gần nhất do GHN báo (ready_to_pick, delivering, delivered…).</summary>
    public string? GhnStatus { get; private set; }

    /// <summary>Ngày giao dự kiến do GHN trả khi tạo vận đơn.</summary>
    public DateTime? GhnExpectedDeliveryTime { get; private set; }

    /// <summary>Thời điểm trạng thái giao hàng được cập nhật gần nhất (webhook hoặc làm mới thủ công).</summary>
    public DateTime? GhnStatusTime { get; private set; }

    /// <summary>Số tiền khách phải trả: tiền hàng + phí vận chuyển (thu hộ COD đúng số này).</summary>
    public decimal PayableAmount => TotalAmount + ShippingFee;

    public List<OrderItem> Items { get; private set; } = new();

    private Order() { }

    public Order(long userId, string receiverName, string phoneNumber, string shippingAddress,
        PaymentMethod paymentMethod, string? note, DeliveryMethod deliveryMethod = DeliveryMethod.Shipping)
    {
        UserId = userId;
        ReceiverName = receiverName;
        PhoneNumber = phoneNumber;
        ShippingAddress = shippingAddress;
        PaymentMethod = paymentMethod;
        Note = note;
        DeliveryMethod = deliveryMethod;
        Status = OrderStatus.Pending;
    }

    public bool IsStorePickup => DeliveryMethod == DeliveryMethod.StorePickup;

    /// <summary>Mã đơn hiển thị cho khách, VD: HSV000123.</summary>
    public static string FormatCode(int id) => $"HSV{id:D6}";

    /// <summary>Ghi địa chỉ nhận (mã tỉnh / phường của GHN, số nhà) và phí vận chuyển đã được server tính.</summary>
    public void SetShipping(decimal fee, int provinceId, string wardCode, string street)
    {
        if (IsStorePickup)
            throw new UserFriendlyException("Đơn nhận tại cửa hàng không có địa chỉ giao hàng.");
        ShippingFee = fee;
        ToProvinceId = provinceId;
        ToWardCode = wardCode;
        ToStreet = street;
        ToDistrictId = null;
    }

    /// <summary>Lưu vận đơn vừa tạo trên GHN.</summary>
    public void AttachShipment(string ghnOrderCode, DateTime? expectedDeliveryTime)
    {
        if (IsStorePickup)
            throw new UserFriendlyException($"Đơn {FormatCode(Id)} nhận tại cửa hàng, không cần tạo vận đơn.");
        if (!string.IsNullOrEmpty(GhnOrderCode))
            throw new UserFriendlyException($"Đơn {FormatCode(Id)} đã có vận đơn {GhnOrderCode}.");
        GhnOrderCode = ghnOrderCode;
        GhnStatus = "ready_to_pick";
        GhnExpectedDeliveryTime = expectedDeliveryTime;
        GhnStatusTime = DateTime.Now;
    }

    /// <summary>Cập nhật trạng thái giao hàng theo GHN; bỏ qua nếu không đổi. Trả true khi có thay đổi.</summary>
    public bool UpdateShipmentStatus(string ghnStatus, DateTime? statusTime = null)
    {
        if (string.IsNullOrEmpty(GhnOrderCode) || GhnStatus == ghnStatus)
            return false;
        GhnStatus = ghnStatus;
        GhnStatusTime = statusTime ?? DateTime.Now;
        return true;
    }

    /// <summary>Gỡ vận đơn đã hủy trên GHN để có thể tạo lại.</summary>
    public void DetachShipment()
    {
        GhnOrderCode = null;
        GhnStatus = null;
        GhnExpectedDeliveryTime = null;
        GhnStatusTime = DateTime.Now;
    }

    /// <param name="unitCost">Giá vốn bình quân của biến thể lúc bán (null = chưa có giá vốn).</param>
    public void AddItem(int productVariantId, string productName, string variantName, int quantity, decimal unitPrice,
        decimal? unitCost = null)
    {
        Items.Add(new OrderItem
        {
            ProductVariantId = productVariantId,
            ProductName = productName,
            VariantName = variantName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            UnitCost = unitCost
        });
        TotalAmount = Items.Sum(i => i.UnitPrice * i.Quantity);
    }

    /// <summary>
    /// Chuyển trạng thái theo đúng thứ tự. Giao tận nơi: Pending → Shipping → Completed; nhận tại cửa hàng:
    /// Pending → ReadyForPickup → Completed. Hủy được trước khi hoàn thành.
    /// Không cho lùi trạng thái hay thay đổi đơn đã kết thúc.
    /// </summary>
    public void ChangeStatus(OrderStatus newStatus)
    {
        // Bước giữa của từng hình thức: "Đang giao" chỉ cho đơn giao tận nơi, "Chờ khách nhận" chỉ cho đơn nhận tại cửa hàng.
        var middle = IsStorePickup ? OrderStatus.ReadyForPickup : OrderStatus.Shipping;
        var allowed = (Status, newStatus) switch
        {
            (OrderStatus.Pending, var s) when s == middle => true,
            (var s, OrderStatus.Completed) when s == middle => true,
            (OrderStatus.Pending, OrderStatus.Canceled) => true,
            (var s, OrderStatus.Canceled) when s == middle => true,
            _ => false
        };
        if (!allowed)
            throw new UserFriendlyException(
                $"Không thể chuyển đơn {FormatCode(Id)} từ trạng thái {Status} sang {newStatus}.");

        Status = newStatus;
        LastStatusChangeTime = DateTime.Now;
    }
}

public class OrderItem : Entity<int>
{
    public int OrderId { get; set; }
    public int ProductVariantId { get; set; }

    // Ảnh chụp thông tin tại thời điểm mua — lịch sử đơn không đổi khi sản phẩm bị sửa/ẩn sau này.
    public string ProductName { get; set; } = null!;
    public string VariantName { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Giá vốn một sản phẩm lúc bán (chụp từ ProductVariant.AverageCost) — để tính lợi nhuận.
    /// null = đơn bán trước khi có chức năng nhập kho, hoặc biến thể chưa có phiếu nhập.
    /// </summary>
    public decimal? UnitCost { get; set; }
}
