using HornViet.Domain.Orders.Entities;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Orders.Enums;

namespace HornViet.Domain.UnitTests.Orders;

public class OrderTests
{
    private static Order NewOrder() =>
        new(userId: 1, receiverName: "Khách test", phoneNumber: "0900000000", shippingAddress: "Địa chỉ test",
            paymentMethod: PaymentMethod.COD, note: null);

    [Fact]
    public void NewOrder_StartsPending()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.LastStatusChangeTime);
    }

    [Fact]
    public void AddItem_RecalculatesTotalAmount()
    {
        var order = NewOrder();

        order.AddItem(productVariantId: 1, "Lược sừng", "Mặc định", quantity: 2, unitPrice: 100_000);
        order.AddItem(productVariantId: 2, "Trâm cài", "Hoa sen", quantity: 1, unitPrice: 50_000);

        Assert.Equal(2, order.Items.Count);
        Assert.Equal(250_000, order.TotalAmount);
    }

    [Fact]
    public void ChangeStatus_FollowsPendingShippingCompleted()
    {
        var order = NewOrder();

        order.ChangeStatus(OrderStatus.Shipping);
        order.ChangeStatus(OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.LastStatusChangeTime);
    }

    [Theory]
    [InlineData(false)] // hủy khi đang chờ xử lý
    [InlineData(true)]  // hủy khi đang giao
    public void ChangeStatus_CanCancelFromPendingOrShipping(bool shipFirst)
    {
        var order = NewOrder();
        if (shipFirst) order.ChangeStatus(OrderStatus.Shipping);

        order.ChangeStatus(OrderStatus.Canceled);

        Assert.Equal(OrderStatus.Canceled, order.Status);
    }

    [Fact]
    public void ChangeStatus_CannotSkipShipping()
    {
        var order = NewOrder();

        Assert.Throws<UserFriendlyException>(() => order.ChangeStatus(OrderStatus.Completed));
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipping)]
    [InlineData(OrderStatus.Canceled)]
    public void ChangeStatus_CompletedOrderIsFinal(OrderStatus target)
    {
        var order = NewOrder();
        order.ChangeStatus(OrderStatus.Shipping);
        order.ChangeStatus(OrderStatus.Completed);

        Assert.Throws<UserFriendlyException>(() => order.ChangeStatus(target));
    }

    [Fact]
    public void ChangeStatus_CanceledOrderCannotBeReopened()
    {
        var order = NewOrder();
        order.ChangeStatus(OrderStatus.Canceled);

        Assert.Throws<UserFriendlyException>(() => order.ChangeStatus(OrderStatus.Shipping));
    }

    [Fact]
    public void FormatCode_PadsToSixDigits()
    {
        Assert.Equal("HSV000123", Order.FormatCode(123));
    }

    // ---- Nhận tại cửa hàng

    private static Order NewPickupOrder() =>
        new(userId: 1, receiverName: "Khách test", phoneNumber: "0900000000", shippingAddress: "Nhận tại cửa hàng",
            paymentMethod: PaymentMethod.COD, note: null, deliveryMethod: DeliveryMethod.StorePickup);

    [Fact]
    public void Pickup_FollowsPendingReadyForPickupCompleted()
    {
        var order = NewPickupOrder();

        order.ChangeStatus(OrderStatus.ReadyForPickup);
        order.ChangeStatus(OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(0, order.ShippingFee);
    }

    [Fact]
    public void Pickup_CannotUseShippingStatus()
    {
        var order = NewPickupOrder();

        Assert.Throws<UserFriendlyException>(() => order.ChangeStatus(OrderStatus.Shipping));
        Assert.Throws<UserFriendlyException>(() => order.ChangeStatus(OrderStatus.Completed));
    }

    [Fact]
    public void Shipping_CannotUseReadyForPickup()
    {
        var order = NewOrder();

        Assert.Throws<UserFriendlyException>(() => order.ChangeStatus(OrderStatus.ReadyForPickup));
    }

    [Theory]
    [InlineData(false)] // hủy khi đang chờ xử lý
    [InlineData(true)]  // hủy khi hàng đã chuẩn bị, khách chưa đến nhận
    public void Pickup_CanCancelBeforeCompleted(bool readyFirst)
    {
        var order = NewPickupOrder();
        if (readyFirst) order.ChangeStatus(OrderStatus.ReadyForPickup);

        order.ChangeStatus(OrderStatus.Canceled);

        Assert.Equal(OrderStatus.Canceled, order.Status);
    }

    [Fact]
    public void Pickup_HasNoShipment()
    {
        var order = NewPickupOrder();

        Assert.Throws<UserFriendlyException>(() => order.SetShipping(30_000, 1, "W1", "Số 1"));
        Assert.Throws<UserFriendlyException>(() => order.AttachShipment("GHN1", null));
    }
}
