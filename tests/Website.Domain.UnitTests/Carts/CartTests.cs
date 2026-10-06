using Website.Domain.Carts.Entities;
using Website.Domain.Catalog.Entities;
using Website.Domain.SharedKernel;

namespace Website.Domain.UnitTests.Carts;

public class CartTests
{
    private static ProductVariant Variant(int id, int stock) => new("Nhỏ", null, stock) { Id = id };

    [Fact]
    public void AddOrIncrease_AddsNewItem()
    {
        var cart = new Cart { UserId = 1 };

        var item = cart.AddOrIncrease(Variant(10, stock: 5), 2);

        Assert.Single(cart.Items);
        Assert.Equal(10, item.ProductVariantId);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void AddOrIncrease_SameVariant_AccumulatesQuantity()
    {
        var cart = new Cart { UserId = 1 };
        var variant = Variant(10, stock: 5);

        cart.AddOrIncrease(variant, 2);
        cart.AddOrIncrease(variant, 1);

        Assert.Single(cart.Items);
        Assert.Equal(3, cart.Items[0].Quantity);
    }

    [Fact]
    public void AddOrIncrease_BeyondStock_Throws()
    {
        var cart = new Cart { UserId = 1 };
        var variant = Variant(10, stock: 3);
        cart.AddOrIncrease(variant, 2);

        Assert.Throws<UserFriendlyException>(() => cart.AddOrIncrease(variant, 2));
        Assert.Equal(2, cart.Items[0].Quantity);
    }

    [Fact]
    public void AddOrIncrease_OutOfStock_Throws()
    {
        var cart = new Cart { UserId = 1 };

        Assert.Throws<UserFriendlyException>(() => cart.AddOrIncrease(Variant(10, stock: 0), 1));
        Assert.Empty(cart.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AddOrIncrease_RejectsNonPositiveQuantity(int quantity)
    {
        var cart = new Cart { UserId = 1 };

        Assert.Throws<UserFriendlyException>(() => cart.AddOrIncrease(Variant(10, stock: 5), quantity));
    }
}
