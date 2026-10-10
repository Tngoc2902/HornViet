using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.Inventory.Entities;
using HornViet.Domain.Orders.Entities;
using HornViet.Domain.Orders.Enums;
using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.UnitTests.Inventory;

public class StockReceiptTests
{
    private static ProductVariant NewVariant(int stock = 0) => new("Nhỏ", null, stock) { Id = 1 };

    [Fact]
    public void FirstReceipt_SetsAverageCostToUnitCost()
    {
        var variant = NewVariant();

        StockReceipt.Receive(variant, 10, 300, null, creatorUserId: 1);

        Assert.Equal(10, variant.StockQuantity);
        Assert.Equal(300, variant.AverageCost);
    }

    // Bài toán: 1–10/1 nhập giá 300 bán 10 sp; 11–30/1 nhập giá 400 bán 20 sp; giá bán 500.
    [Fact]
    public void SellOutEachBatch_ProfitUsesEachBatchCost()
    {
        var variant = NewVariant();
        var order = new Order(1, "Khách test", "0900000000", "Địa chỉ test", PaymentMethod.COD, null);

        StockReceipt.Receive(variant, 10, 300, null, 1);
        order.AddItem(variant.Id, "Lược sừng", variant.DisplayName, 10, 500, variant.AverageCost);
        variant.DecreaseStock(10);

        StockReceipt.Receive(variant, 20, 400, null, 1);
        order.AddItem(variant.Id, "Lược sừng", variant.DisplayName, 20, 500, variant.AverageCost);
        variant.DecreaseStock(20);

        var revenue = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        var cost = order.Items.Sum(i => i.UnitCost!.Value * i.Quantity);
        Assert.Equal(15_000, revenue);
        Assert.Equal(11_000, cost);
        Assert.Equal(4_000, revenue - cost);
    }

    [Fact]
    public void LeftoverStock_UsesMovingAverage()
    {
        var variant = NewVariant();

        StockReceipt.Receive(variant, 20, 300, null, 1);
        variant.DecreaseStock(10);                       // còn 10 sp giá 300
        StockReceipt.Receive(variant, 30, 400, null, 1); // (10×300 + 30×400) / 40

        Assert.Equal(40, variant.StockQuantity);
        Assert.Equal(375, variant.AverageCost);
    }

    [Fact]
    public void LegacyStockWithoutCost_TakesFirstReceiptCost()
    {
        var variant = NewVariant(stock: 5); // hàng tồn từ trước khi có chức năng nhập kho

        StockReceipt.Receive(variant, 10, 300, null, 1);

        Assert.Equal(15, variant.StockQuantity);
        Assert.Equal(300, variant.AverageCost);
    }

    [Fact]
    public void CorrectLatestReceipt_RecomputesAverageAfterSales()
    {
        var variant = NewVariant();
        StockReceipt.Receive(variant, 20, 300, null, 1);
        variant.DecreaseStock(10);
        var receipt = StockReceipt.Receive(variant, 30, 4_000, null, 1); // gõ nhầm 4.000 thay vì 400
        variant.DecreaseStock(5);                                       // đã bán thêm 5 sp

        receipt.Correct(30, 400, "Sửa giá nhập", modifierUserId: 1, DateTime.Now);

        Assert.Equal(35, variant.StockQuantity);
        Assert.Equal(375, variant.AverageCost);
        Assert.Equal(400, receipt.UnitCost);
        Assert.NotNull(receipt.LastModificationTime);
    }

    [Fact]
    public void CorrectLatestReceipt_ChangesStockByQuantityDifference()
    {
        var variant = NewVariant();
        var receipt = StockReceipt.Receive(variant, 30, 400, null, 1);

        receipt.Correct(25, 400, null, 1, DateTime.Now);

        Assert.Equal(25, variant.StockQuantity);
    }

    [Fact]
    public void CorrectLatestReceipt_CannotDropBelowSoldQuantity()
    {
        var variant = NewVariant();
        var receipt = StockReceipt.Receive(variant, 30, 400, null, 1);
        variant.DecreaseStock(25); // còn 5

        Assert.Throws<UserFriendlyException>(() => receipt.Correct(20, 400, null, 1, DateTime.Now));
        Assert.Equal(5, variant.StockQuantity);
        Assert.Equal(30, receipt.Quantity);
    }

    [Fact]
    public void RevertLatestReceipt_RestoresStockAndAverage()
    {
        var variant = NewVariant();
        StockReceipt.Receive(variant, 10, 300, null, 1);
        var receipt = StockReceipt.Receive(variant, 30, 400, null, 1);

        receipt.Revert();

        Assert.Equal(10, variant.StockQuantity);
        Assert.Equal(300, variant.AverageCost);
    }

    [Fact]
    public void RevertOnlyReceipt_ClearsAverageCost()
    {
        var variant = NewVariant();
        var receipt = StockReceipt.Receive(variant, 10, 300, null, 1);

        receipt.Revert();

        Assert.Equal(0, variant.StockQuantity);
        Assert.Null(variant.AverageCost);
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(-1, 300)]
    [InlineData(10, -1)]
    public void Receive_RejectsInvalidQuantityOrCost(int quantity, decimal unitCost)
    {
        var variant = NewVariant();

        Assert.Throws<UserFriendlyException>(() => StockReceipt.Receive(variant, quantity, unitCost, null, 1));
        Assert.Equal(0, variant.StockQuantity);
        Assert.Null(variant.AverageCost);
    }

    [Fact]
    public void SalesAndStockCount_DoNotChangeAverageCost()
    {
        var variant = NewVariant();
        StockReceipt.Receive(variant, 10, 300, null, 1);

        variant.DecreaseStock(4);
        variant.IncreaseStock(2);
        variant.SetStock(20);

        Assert.Equal(300, variant.AverageCost);
    }
}
