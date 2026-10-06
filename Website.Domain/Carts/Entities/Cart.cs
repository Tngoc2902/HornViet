using Website.Domain.Catalog.Entities;
using Website.Domain.SharedKernel;

namespace Website.Domain.Carts.Entities;

/// <summary>Mỗi người dùng có đúng một giỏ hàng (tạo khi thêm sản phẩm lần đầu).</summary>
public class Cart : Entity<int>, IHasCreationTime
{
    public long UserId { get; set; }
    public DateTime CreationTime { get; set; }
    public List<CartItem> Items { get; set; } = new();

    /// <summary>Thêm mới hoặc cộng dồn số lượng nếu biến thể đã có trong giỏ.</summary>
    public CartItem AddOrIncrease(ProductVariant variant, int quantity)
    {
        if (quantity <= 0)
            throw new UserFriendlyException("Số lượng phải lớn hơn 0.");

        var item = Items.FirstOrDefault(i => i.ProductVariantId == variant.Id);
        var newQuantity = (item?.Quantity ?? 0) + quantity;
        EnsureInStock(variant, newQuantity);

        if (item == null)
        {
            item = new CartItem { ProductVariantId = variant.Id, ProductVariant = variant, Quantity = newQuantity };
            Items.Add(item);
        }
        else
        {
            item.Quantity = newQuantity;
        }
        return item;
    }

    public static void EnsureInStock(ProductVariant variant, int quantity)
    {
        if (variant.StockQuantity <= 0)
            throw new UserFriendlyException($"Biến thể \"{variant.DisplayName}\" đã hết hàng.");
        if (quantity > variant.StockQuantity)
            throw new UserFriendlyException(
                $"Biến thể \"{variant.DisplayName}\" chỉ còn {variant.StockQuantity} sản phẩm.");
    }
}

public class CartItem : Entity<int>
{
    public int CartId { get; set; }
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int Quantity { get; set; }
}
