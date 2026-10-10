using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.Inventory.Entities;

/// <summary>
/// Phiếu nhập kho (chỉ Quản trị viên): nhập thêm hàng cho một biến thể với giá nhập cụ thể. Mỗi phiếu cộng tồn kho và
/// tính lại giá vốn bình quân của biến thể; giá vốn này được chụp vào OrderItem.UnitCost khi bán để tính lợi nhuận.
/// </summary>
public class StockReceipt : Entity<int>, IHasCreationTime
{
    public const int MaxQuantity = 1_000_000;
    public const decimal MaxUnitCost = 999_999_999;

    public int ProductVariantId { get; private set; }
    public ProductVariant ProductVariant { get; private set; } = null!;

    /// <summary>Số lượng nhập.</summary>
    public int Quantity { get; private set; }

    /// <summary>Giá nhập một sản phẩm (VND).</summary>
    public decimal UnitCost { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Tồn kho của biến thể ngay trước khi nhập — dùng để tính lại khi sửa / hủy phiếu.</summary>
    public int StockBefore { get; private set; }

    /// <summary>Giá vốn bình quân ngay trước khi nhập (null = chưa có) — dùng để tính lại khi sửa / hủy phiếu.</summary>
    public decimal? AverageCostBefore { get; private set; }

    public long CreatorUserId { get; private set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; private set; }
    public long? LastModifierUserId { get; private set; }

    private StockReceipt() { }

    /// <summary>Tạo phiếu nhập và cập nhật ngay tồn kho, giá vốn bình quân của biến thể.</summary>
    public static StockReceipt Receive(ProductVariant variant, int quantity, decimal unitCost, string? note, long creatorUserId)
    {
        Validate(quantity, unitCost);
        var receipt = new StockReceipt
        {
            ProductVariantId = variant.Id,
            ProductVariant = variant,
            Quantity = quantity,
            UnitCost = unitCost,
            Note = note,
            StockBefore = variant.StockQuantity,
            AverageCostBefore = variant.AverageCost,
            CreatorUserId = creatorUserId
        };
        variant.ReceiveStock(quantity, unitCost);
        return receipt;
    }

    /// <summary>Sửa số lượng / giá nhập. Chỉ dùng cho phiếu mới nhất của biến thể (tầng Application kiểm tra).</summary>
    public void Correct(int quantity, decimal unitCost, string? note, long modifierUserId, DateTime now)
    {
        Validate(quantity, unitCost);
        ProductVariant.ReplaceLatestReceipt(StockBefore, AverageCostBefore, Quantity, quantity, unitCost);
        Quantity = quantity;
        UnitCost = unitCost;
        Note = note;
        LastModifierUserId = modifierUserId;
        LastModificationTime = now;
    }

    /// <summary>Hủy phiếu: trừ lại số lượng đã nhập và trả giá vốn bình quân về như trước khi nhập.</summary>
    public void Revert() => ProductVariant.ReplaceLatestReceipt(StockBefore, AverageCostBefore, Quantity, 0, 0);

    private static void Validate(int quantity, decimal unitCost)
    {
        if (quantity <= 0 || quantity > MaxQuantity)
            throw new UserFriendlyException($"Số lượng nhập phải từ 1 đến {MaxQuantity:N0}.");
        if (unitCost < 0 || unitCost > MaxUnitCost)
            throw new UserFriendlyException("Giá nhập không hợp lệ.");
    }
}
