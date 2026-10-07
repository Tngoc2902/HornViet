using Website.Domain.SharedKernel;
using Website.Domain.Orders.Enums;

namespace Website.Domain.Orders.Entities;

/// <summary>Aggregate root đơn hàng: giữ máy trạng thái và tổng tiền.</summary>
public class Order : Entity<int>, IHasCreationTime
{
    public long UserId { get; private set; }
    public string ReceiverName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string ShippingAddress { get; private set; } = null!;
    public decimal TotalAmount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string? Note { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastStatusChangeTime { get; private set; }

    public List<OrderItem> Items { get; private set; } = new();

    private Order() { }

    public Order(long userId, string receiverName, string phoneNumber, string shippingAddress,
        PaymentMethod paymentMethod, string? note)
    {
        UserId = userId;
        ReceiverName = receiverName;
        PhoneNumber = phoneNumber;
        ShippingAddress = shippingAddress;
        PaymentMethod = paymentMethod;
        Note = note;
        Status = OrderStatus.Pending;
    }

    /// <summary>Mã đơn hiển thị cho khách, VD: HSV000123.</summary>
    public static string FormatCode(int id) => $"HSV{id:D6}";

    public void AddItem(int productVariantId, string productName, string variantName, int quantity, decimal unitPrice)
    {
        Items.Add(new OrderItem
        {
            ProductVariantId = productVariantId,
            ProductName = productName,
            VariantName = variantName,
            Quantity = quantity,
            UnitPrice = unitPrice
        });
        TotalAmount = Items.Sum(i => i.UnitPrice * i.Quantity);
    }

    /// <summary>
    /// Chuyển trạng thái theo đúng thứ tự: Pending → Shipping → Completed; hủy được từ Pending hoặc Shipping.
    /// Không cho lùi trạng thái hay thay đổi đơn đã kết thúc.
    /// </summary>
    public void ChangeStatus(OrderStatus newStatus)
    {
        var allowed = (Status, newStatus) switch
        {
            (OrderStatus.Pending, OrderStatus.Shipping) => true,
            (OrderStatus.Shipping, OrderStatus.Completed) => true,
            (OrderStatus.Pending, OrderStatus.Canceled) => true,
            (OrderStatus.Shipping, OrderStatus.Canceled) => true,
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
}
