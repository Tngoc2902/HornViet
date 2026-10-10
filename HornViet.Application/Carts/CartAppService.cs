using System.ComponentModel.DataAnnotations;
using HornViet.Application.Common;
using HornViet.Domain.Carts.Entities;
using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Carts;

public class CartItemDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string MainImage { get; set; } = null!;
    public string VariantName { get; set; } = null!;
    public string? Size { get; set; }
    public string? Style { get; set; }
    /// <summary>Giá hiện tại của sản phẩm (tổng tạm tính luôn tính theo giá hiện tại).</summary>
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public int StockQuantity { get; set; }
    /// <summary>false khi sản phẩm đã ngừng bán hoặc không đủ hàng — giao diện cần cảnh báo trước khi đặt.</summary>
    public bool IsAvailable { get; set; }
}

public class CartDto
{
    public IReadOnlyList<CartItemDto> Items { get; set; } = Array.Empty<CartItemDto>();
    public int TotalQuantity { get; set; }
    public decimal SubTotal { get; set; }
    public bool CanCheckout { get; set; }
}

public class AddCartItemInput
{
    [Range(1, int.MaxValue)]
    public int ProductVariantId { get; set; }

    [Range(1, 999, ErrorMessage = "Số lượng không hợp lệ.")]
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemInput
{
    [Range(1, int.MaxValue)]
    public int CartItemId { get; set; }

    [Range(1, 999, ErrorMessage = "Số lượng không hợp lệ.")]
    public int Quantity { get; set; }
}

public interface ICartAppService
{
    Task<CartDto> GetMyCartAsync();
    Task<CartDto> AddItemAsync(AddCartItemInput input);
    Task<CartDto> UpdateItemAsync(UpdateCartItemInput input);
    Task<CartDto> DeleteItemAsync(int cartItemId);
}

public class CartAppService : ICartAppService
{
    private readonly IRepository<Cart, int> _cartRepository;
    private readonly IRepository<ProductVariant, int> _variantRepository;
    private readonly IRepository<CartItem, int> _cartItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;

    public CartAppService(IRepository<Cart, int> cartRepository, IRepository<ProductVariant, int> variantRepository,
        IRepository<CartItem, int> cartItemRepository, IUnitOfWork unitOfWork, IAppSession session)
    {
        _cartRepository = cartRepository;
        _variantRepository = variantRepository;
        _cartItemRepository = cartItemRepository;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    public async Task<CartDto> GetMyCartAsync() => ToDto(await LoadCartAsync());

    public async Task<CartDto> AddItemAsync(AddCartItemInput input)
    {
        var variant = await _variantRepository.GetAll()
                          .Include(v => v.Product)
                          .FirstOrDefaultAsync(v => v.Id == input.ProductVariantId)
                      ?? throw new EntityNotFoundException("Biến thể sản phẩm không tồn tại.");
        if (!variant.Product.IsActive)
            throw new UserFriendlyException("Sản phẩm đã ngừng kinh doanh.");

        var cart = await LoadCartAsync();
        if (cart.Id == 0)
            await _cartRepository.InsertAsync(cart);

        cart.AddOrIncrease(variant, input.Quantity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(cart);
    }

    /// <summary>Số lượng vượt tồn kho sẽ được giới hạn về số lượng còn lại của biến thể (UC 3.2.2.7 – 3a).</summary>
    public async Task<CartDto> UpdateItemAsync(UpdateCartItemInput input)
    {
        var cart = await LoadCartAsync();
        var item = cart.Items.FirstOrDefault(i => i.Id == input.CartItemId)
                   ?? throw new EntityNotFoundException("Sản phẩm không có trong giỏ hàng.");

        var stock = item.ProductVariant.StockQuantity;
        if (stock <= 0)
            throw new UserFriendlyException($"Biến thể \"{item.ProductVariant.DisplayName}\" đã hết hàng, vui lòng xóa khỏi giỏ.");

        item.Quantity = Math.Min(input.Quantity, stock);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(cart);
    }

    public async Task<CartDto> DeleteItemAsync(int cartItemId)
    {
        var cart = await LoadCartAsync();
        var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId)
                   ?? throw new EntityNotFoundException("Sản phẩm không có trong giỏ hàng.");
        cart.Items.Remove(item);
        _cartItemRepository.Delete(item);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(cart);
    }

    /// <summary>Giỏ của người dùng hiện tại; chưa có thì trả về giỏ rỗng (chưa lưu, Id = 0).</summary>
    private async Task<Cart> LoadCartAsync()
    {
        var userId = _session.GetUserId();
        return await _cartRepository.GetAll()
                   .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product)
                   .FirstOrDefaultAsync(c => c.UserId == userId)
               ?? new Cart { UserId = userId };
    }

    private static CartDto ToDto(Cart cart)
    {
        // Mục có biến thể/sản phẩm đã bị xóa mềm bị bộ lọc loại khỏi Include — bỏ qua an toàn.
        var items = cart.Items
            .Where(i => i.ProductVariant?.Product != null)
            .OrderBy(i => i.Id)
            .Select(i =>
            {
                var v = i.ProductVariant;
                return new CartItemDto
                {
                    Id = i.Id,
                    ProductVariantId = v.Id,
                    ProductId = v.ProductId,
                    ProductName = v.Product.Name,
                    MainImage = v.Product.MainImage,
                    VariantName = v.DisplayName,
                    Size = v.Size,
                    Style = v.Style,
                    UnitPrice = v.Product.Price,
                    Quantity = i.Quantity,
                    LineTotal = v.Product.Price * i.Quantity,
                    StockQuantity = v.StockQuantity,
                    IsAvailable = v.Product.IsActive && v.StockQuantity >= i.Quantity
                };
            })
            .ToList();

        return new CartDto
        {
            Items = items,
            TotalQuantity = items.Sum(i => i.Quantity),
            SubTotal = items.Sum(i => i.LineTotal),
            CanCheckout = items.Count > 0 && items.All(i => i.IsAvailable)
        };
    }
}
