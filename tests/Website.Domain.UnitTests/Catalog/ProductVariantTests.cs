using Website.Domain.Catalog.Entities;
using Website.Domain.SharedKernel;

namespace Website.Domain.UnitTests.Catalog;

public class ProductVariantTests
{
    [Fact]
    public void Constructor_RejectsNegativeStock()
    {
        Assert.Throws<UserFriendlyException>(() => new ProductVariant("Nhỏ", null, -1));
    }

    [Fact]
    public void DecreaseStock_ReducesQuantity()
    {
        var variant = new ProductVariant("Nhỏ", null, 5);

        variant.DecreaseStock(3);

        Assert.Equal(2, variant.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_MoreThanAvailable_ThrowsAndKeepsStock()
    {
        var variant = new ProductVariant("Nhỏ", null, 2);

        Assert.Throws<UserFriendlyException>(() => variant.DecreaseStock(3));
        Assert.Equal(2, variant.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DecreaseStock_RejectsNonPositiveQuantity(int quantity)
    {
        var variant = new ProductVariant("Nhỏ", null, 5);

        Assert.Throws<UserFriendlyException>(() => variant.DecreaseStock(quantity));
    }

    [Fact]
    public void IncreaseStock_IgnoresNonPositiveQuantity()
    {
        var variant = new ProductVariant("Nhỏ", null, 5);

        variant.IncreaseStock(0);
        variant.IncreaseStock(-3);
        variant.IncreaseStock(2);

        Assert.Equal(7, variant.StockQuantity);
    }

    [Fact]
    public void SetStock_RejectsNegative()
    {
        var variant = new ProductVariant("Nhỏ", null, 5);

        Assert.Throws<UserFriendlyException>(() => variant.SetStock(-1));
        Assert.Equal(5, variant.StockQuantity);
    }

    [Theory]
    [InlineData(null, null, "Mặc định")]
    [InlineData("  ", null, "Mặc định")]
    [InlineData("Vừa", null, "Vừa")]
    [InlineData(null, "Hoa sen", "Hoa sen")]
    [InlineData("Nhỏ", "Răng thưa", "Nhỏ / Răng thưa")]
    public void DisplayName_JoinsSizeAndStyle(string? size, string? style, string expected)
    {
        Assert.Equal(expected, new ProductVariant(size, style, 1).DisplayName);
    }
}
