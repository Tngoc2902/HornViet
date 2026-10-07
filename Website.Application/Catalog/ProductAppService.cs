using Website.Application.Common;
using Website.Domain;
using Website.Domain.Carts.Entities;
using Website.Domain.Catalog.Entities;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.Orders.Entities;
using Website.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Website.Application.Catalog.Enums;
using Website.Domain.Orders.Enums;

namespace Website.Application.Catalog;

public interface IProductAppService
{
    Task<PagedResultDto<ProductListItemDto>> GetAllAsync(GetProductsInput input);
    Task<ProductDetailDto> GetAsync(int id);
    Task<PagedResultDto<ProductAdminListItemDto>> GetAdminListAsync(GetProductsForAdminInput input);
    Task<ProductDetailDto> CreateAsync(CreateOrUpdateProductInput input);
    Task<ProductDetailDto> UpdateAsync(CreateOrUpdateProductInput input);
    Task SetActiveAsync(SetProductActiveInput input);
    Task DeleteAsync(int id);
}

public class ProductAppService : IProductAppService
{
    private const string NotAvailableMessage = "Sản phẩm không còn tồn tại hoặc đã ngừng kinh doanh.";

    private readonly IRepository<Product, int> _productRepository;
    private readonly IRepository<Category, int> _categoryRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<CartItem, int> _cartItemRepository;
    private readonly IRepository<Order, int> _orderRepository;
    private readonly IRepository<OrderItem, int> _orderItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;

    public ProductAppService(
        IRepository<Product, int> productRepository,
        IRepository<Category, int> categoryRepository,
        IRepository<Material, int> materialRepository,
        IRepository<CartItem, int> cartItemRepository,
        IRepository<Order, int> orderRepository,
        IRepository<OrderItem, int> orderItemRepository,
        IUnitOfWork unitOfWork,
        IAppSession session)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _materialRepository = materialRepository;
        _cartItemRepository = cartItemRepository;
        _orderRepository = orderRepository;
        _orderItemRepository = orderItemRepository;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    // ------------------------------------------------------------------ Khách hàng

    public async Task<PagedResultDto<ProductListItemDto>> GetAllAsync(GetProductsInput input)
    {
        var query = _productRepository.GetAll().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            query = query.Where(p => p.Name.Contains(keyword) || p.Category.Name.Contains(keyword) ||
                                     p.Material.Name.Contains(keyword));
        }
        if (input.CategoryIds is { Count: > 0 })
            query = query.Where(p => input.CategoryIds.Contains(p.CategoryId));
        if (input.MaterialIds is { Count: > 0 })
            query = query.Where(p => input.MaterialIds.Contains(p.MaterialId));
        if (input.MinPrice.HasValue)
            query = query.Where(p => p.Price >= input.MinPrice.Value);
        if (input.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= input.MaxPrice.Value);
        if (input.InStockOnly)
            query = query.Where(p => p.Variants.Any(v => v.StockQuantity > 0));

        var totalCount = await query.CountAsync();

        // Quy tắc nghiệp vụ: sản phẩm còn hàng luôn được ưu tiên hiển thị trước.
        var ordered = query.OrderByDescending(p => p.Variants.Any(v => v.StockQuantity > 0));
        ordered = input.Sorting switch
        {
            ProductSorting.PriceAsc => ordered.ThenBy(p => p.Price),
            ProductSorting.PriceDesc => ordered.ThenByDescending(p => p.Price),
            ProductSorting.NameAsc => ordered.ThenBy(p => p.Name),
            ProductSorting.BestSelling => ordered.ThenByDescending(p => _orderItemRepository.GetAll()
                .Where(oi => p.Variants.Select(v => v.Id).Contains(oi.ProductVariantId) &&
                             _orderRepository.GetAll().Any(o => o.Id == oi.OrderId && o.Status == OrderStatus.Completed))
                .Sum(oi => (int?)oi.Quantity) ?? 0),
            _ => ordered.ThenByDescending(p => p.CreationTime)
        };

        var items = await ordered.ThenBy(p => p.Id)
            .PageBy(input)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                MainImage = p.MainImage,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                MaterialId = p.MaterialId,
                MaterialName = p.Material.Name,
                TotalStock = p.Variants.Sum(v => v.StockQuantity),
                InStock = p.Variants.Any(v => v.StockQuantity > 0),
                CreationTime = p.CreationTime
            })
            .ToListAsync();

        return new PagedResultDto<ProductListItemDto>(totalCount, items);
    }

    /// <summary>Khách chỉ xem được sản phẩm đang bán; Nhân viên/Quản trị viên xem được cả sản phẩm đang ẩn.</summary>
    public async Task<ProductDetailDto> GetAsync(int id)
    {
        var product = await _productRepository.GetAll()
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Material)
            .Include(p => p.Variants)
            .Include(p => p.Images)

            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null || (!product.IsActive && !_session.IsManagement()))
            throw new EntityNotFoundException(NotAvailableMessage);

        return ToDetailDto(product);
    }

    // ------------------------------------------------------------------ Quản trị

    public async Task<PagedResultDto<ProductAdminListItemDto>> GetAdminListAsync(GetProductsForAdminInput input)
    {
        var query = _productRepository.GetAll();

        if (!string.IsNullOrWhiteSpace(input.Keyword))
            query = query.Where(p => p.Name.Contains(input.Keyword.Trim()));
        if (input.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == input.CategoryId.Value);
        if (input.IsActive.HasValue)
            query = query.Where(p => p.IsActive == input.IsActive.Value);
        if (input.LowStockOnly)
            query = query.Where(p => p.Variants.Any(v => v.StockQuantity <= AppConsts.LowStockThreshold));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreationTime).ThenByDescending(p => p.Id)
            .PageBy(input)
            .Select(p => new ProductAdminListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                MainImage = p.MainImage,
                Price = p.Price,
                CategoryName = p.Category.Name,
                MaterialName = p.Material.Name,
                VariantCount = p.Variants.Count,
                TotalStock = p.Variants.Sum(v => v.StockQuantity),
                IsLowStock = p.Variants.Any(v => v.StockQuantity <= AppConsts.LowStockThreshold),
                IsActive = p.IsActive,
                CreationTime = p.CreationTime
            })
            .ToListAsync();

        return new PagedResultDto<ProductAdminListItemDto>(totalCount, items);
    }

    public async Task<ProductDetailDto> CreateAsync(CreateOrUpdateProductInput input)
    {
        await ValidateAsync(input);

        var product = new Product();
        MapScalarFields(input, product);
        foreach (var v in input.Variants)
            product.Variants.Add(new ProductVariant(Clean(v.Size), Clean(v.Style), v.StockQuantity));
        ReplaceImages(product, input.Images);

        await _productRepository.InsertAsync(product);
        await _unitOfWork.SaveChangesAsync();
        return await GetAsync(product.Id);
    }

    public async Task<ProductDetailDto> UpdateAsync(CreateOrUpdateProductInput input)
    {
        if (input.Id is not { } id) throw new UserFriendlyException("Thiếu mã sản phẩm.");
        await ValidateAsync(input);

        var product = await _productRepository.GetAll()
                          .Include(p => p.Variants)
                          .Include(p => p.Images)

                          .FirstOrDefaultAsync(p => p.Id == id)
                      ?? throw new EntityNotFoundException("Không tìm thấy sản phẩm.");

        MapScalarFields(input, product);

        // Đồng bộ biến thể: sửa biến thể có Id, thêm biến thể mới, xóa mềm biến thể bị bỏ đi.
        var keepIds = input.Variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        var unknownId = keepIds.FirstOrDefault(vid => product.Variants.All(pv => pv.Id != vid));
        if (unknownId != 0)
            throw new UserFriendlyException($"Biến thể #{unknownId} không thuộc sản phẩm này.");

        var removed = product.Variants.Where(pv => !keepIds.Contains(pv.Id)).ToList();
        foreach (var variant in removed)
            variant.IsDeleted = true;
        if (removed.Count > 0)
        {
            // Biến thể đã ngừng bán thì không thể còn nằm trong giỏ hàng của khách.
            var removedIds = removed.Select(r => r.Id).ToList();
            var staleCartItems = await _cartItemRepository.GetAll()
                .Where(ci => removedIds.Contains(ci.ProductVariantId)).ToListAsync();
            foreach (var ci in staleCartItems) _cartItemRepository.Delete(ci);
        }

        foreach (var v in input.Variants)
        {
            if (v.Id is { } variantId)
            {
                var existing = product.Variants.First(pv => pv.Id == variantId);
                existing.Size = Clean(v.Size);
                existing.Style = Clean(v.Style);
                existing.SetStock(v.StockQuantity);
            }
            else
            {
                product.Variants.Add(new ProductVariant(Clean(v.Size), Clean(v.Style), v.StockQuantity));
            }
        }

        ReplaceImages(product, input.Images);

        await _unitOfWork.SaveChangesAsync();
        return await GetAsync(product.Id);
    }

    /// <summary>Ẩn / hiện sản phẩm trên cửa hàng.</summary>
    public async Task SetActiveAsync(SetProductActiveInput input)
    {
        var product = await _productRepository.GetAll().FirstOrDefaultAsync(p => p.Id == input.Id)
                      ?? throw new EntityNotFoundException("Không tìm thấy sản phẩm.");
        product.IsActive = input.IsActive;
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>Xóa mềm — lịch sử đơn hàng vẫn giữ nguyên nhờ ảnh chụp tên/đơn giá trong OrderItems.</summary>
    public async Task DeleteAsync(int id)
    {
        var product = await _productRepository.GetAll().Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id)
                      ?? throw new EntityNotFoundException("Không tìm thấy sản phẩm.");

        var variantIds = product.Variants.Select(v => v.Id).ToList();
        var cartItems = await _cartItemRepository.GetAll().Where(ci => variantIds.Contains(ci.ProductVariantId)).ToListAsync();
        foreach (var ci in cartItems) _cartItemRepository.Delete(ci);

        _productRepository.Delete(product);
        await _unitOfWork.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Hỗ trợ

    private async Task ValidateAsync(CreateOrUpdateProductInput input)
    {
        if (input.Variants.Count == 0)
            throw new UserFriendlyException("Sản phẩm phải có ít nhất một biến thể.");

        var duplicate = input.Variants
            .GroupBy(v => (Clean(v.Size)?.ToLower(), Clean(v.Style)?.ToLower()))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            throw new UserFriendlyException("Có hai biến thể trùng kích thước và kiểu dáng.");

        if (!await _categoryRepository.GetAll().AnyAsync(c => c.Id == input.CategoryId))
            throw new UserFriendlyException("Danh mục không tồn tại.");
        if (!await _materialRepository.GetAll().AnyAsync(m => m.Id == input.MaterialId))
            throw new UserFriendlyException("Nguyên liệu không tồn tại.");
    }

    private static void MapScalarFields(CreateOrUpdateProductInput input, Product product)
    {
        product.CategoryId = input.CategoryId;
        product.MaterialId = input.MaterialId;
        product.Name = input.Name.Trim();
        product.Price = input.Price;
        product.MainImage = input.MainImage.Trim();
        product.Description = input.Description;
        product.IsActive = input.IsActive;
    }

    private static void ReplaceImages(Product product, IEnumerable<string> urls)
    {
        product.Images.Clear();
        var order = 0;
        foreach (var url in urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
            product.Images.Add(new ProductImage { ImageUrl = url.Trim(), DisplayOrder = order++ });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductDetailDto ToDetailDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        MainImage = p.MainImage,
        Description = p.Description,
        IsActive = p.IsActive,
        CategoryId = p.CategoryId,
        CategoryName = p.Category.Name,
        Material = new ProductMaterialDto
        {
            Id = p.Material.Id, Name = p.Material.Name, Origin = p.Material.Origin, Description = p.Material.Description
        },
        Images = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
        Variants = p.Variants.OrderBy(v => v.Id).Select(ToVariantDto).ToList(),
        TotalStock = p.Variants.Sum(v => v.StockQuantity),
        CreationTime = p.CreationTime
    };

    internal static ProductVariantDto ToVariantDto(ProductVariant v) => new()
    {
        Id = v.Id,
        Size = v.Size,
        Style = v.Style,
        DisplayName = v.DisplayName,
        StockQuantity = v.StockQuantity,
        InStock = v.StockQuantity > 0,
        IsLowStock = v.StockQuantity <= AppConsts.LowStockThreshold
    };
}
