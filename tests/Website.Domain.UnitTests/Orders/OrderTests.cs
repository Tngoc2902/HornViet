using Website.Domain.Orders.Entities;
using Website.Domain.SharedKernel;
using Website.Domain.Orders.Enums;

namespace Website.Domain.UnitTests.Orders;

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
}
