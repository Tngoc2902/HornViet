using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HornViet.Application.Common;
using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Catalog;

public class CollectionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public string? CoverImage { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Số sản phẩm đang bán trong bộ sưu tập (trang quản trị: mọi sản phẩm chưa xóa).</summary>
    public int ProductCount { get; set; }

    /// <summary>Ảnh của vài sản phẩm đầu tiên — dùng làm ảnh bìa dự phòng hoặc ảnh ghép trên trang khách.</summary>
    public List<string> PreviewImages { get; set; } = new();

    /// <summary>Mã sản phẩm theo đúng thứ tự trong bộ (chỉ trả khi xem chi tiết, phục vụ màn sửa của quản trị).</summary>
    public List<int>? ProductIds { get; set; }

    /// <summary>Sản phẩm trong bộ theo thứ tự (chỉ trả khi xem chi tiết). Khách chỉ thấy sản phẩm đang bán.</summary>
    public List<CollectionProductDto>? Products { get; set; }

    public DateTime CreationTime { get; set; }
}

public class CollectionProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string MainImage { get; set; } = null!;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

public class CreateOrUpdateCollectionInput
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên bộ sưu tập."), StringLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>Để trống thì tạo tự động từ tên.</summary>
    [StringLength(120)]
    public string? Slug { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? CoverImage { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    /// <summary>Sản phẩm trong bộ, theo thứ tự hiển thị.</summary>
    public List<int> ProductIds { get; set; } = new();
}

public interface ICollectionAppService
{
    /// <summary>Khách: bộ sưu tập đang bật; nhân viên / quản trị: tất cả.</summary>
    Task<ListResultDto<CollectionDto>> GetAllAsync();

    /// <summary>Chi tiết theo mã (quản trị) hoặc theo slug (trang khách — chỉ bộ đang bật).</summary>
    Task<CollectionDto> GetAsync(int? id, string? slug);

    Task<CollectionDto> CreateAsync(CreateOrUpdateCollectionInput input);
    Task<CollectionDto> UpdateAsync(CreateOrUpdateCollectionInput input);
    Task DeleteAsync(int id);
}

public class CollectionAppService : ICollectionAppService
{
    private const string NotFound = "Không tìm thấy bộ sưu tập.";

    private readonly IRepository<Collection, int> _collectionRepository;
    private readonly IRepository<CollectionProduct, int> _itemRepository;
    private readonly IRepository<Product, int> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;

    public CollectionAppService(IRepository<Collection, int> collectionRepository, IRepository<CollectionProduct, int> itemRepository,
        IRepository<Product, int> productRepository, IUnitOfWork unitOfWork, IAppSession session)
    {
        _collectionRepository = collectionRepository;
        _itemRepository = itemRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    public async Task<ListResultDto<CollectionDto>> GetAllAsync()
    {
        var manage = _session.IsManagement();
        var query = _collectionRepository.GetAll();
        if (!manage)
            query = query.Where(c => c.IsActive);
        var items = await Project(query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name), manage).ToListAsync();
        return new ListResultDto<CollectionDto>(items);
    }

    public async Task<CollectionDto> GetAsync(int? id, string? slug)
    {
        var manage = _session.IsManagement();
        var query = _collectionRepository.GetAll();
        if (id is { } i)
            query = query.Where(c => c.Id == i);
        else if (!string.IsNullOrWhiteSpace(slug))
        {
            var s = slug.Trim().ToLowerInvariant();
            query = query.Where(c => c.Slug == s);
        }
        else
            throw new EntityNotFoundException(NotFound);
        if (!manage)
            query = query.Where(c => c.IsActive);

        var dto = await Project(query, manage).FirstOrDefaultAsync() ?? throw new EntityNotFoundException(NotFound);
        dto.Products = await _itemRepository.GetAll()
            .Where(ci => ci.CollectionId == dto.Id && (manage || ci.Product.IsActive))
            .OrderBy(ci => ci.DisplayOrder)
            .Select(ci => new CollectionProductDto
            {
                Id = ci.ProductId,
                Name = ci.Product.Name,
                MainImage = ci.Product.MainImage,
                Price = ci.Product.Price,
                IsActive = ci.Product.IsActive
            })
            .ToListAsync();
        dto.ProductIds = dto.Products.Select(p => p.Id).ToList();
        return dto;
    }

    public async Task<CollectionDto> CreateAsync(CreateOrUpdateCollectionInput input)
    {
        var collection = new Collection();
        await ApplyAsync(collection, input);
        await _collectionRepository.InsertAsync(collection);
        await _unitOfWork.SaveChangesAsync();
        return await GetAsync(collection.Id, null);
    }

    public async Task<CollectionDto> UpdateAsync(CreateOrUpdateCollectionInput input)
    {
        if (input.Id is not { } id) throw new UserFriendlyException("Thiếu mã bộ sưu tập.");
        var collection = await _collectionRepository.GetAll().Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id)
                         ?? throw new EntityNotFoundException(NotFound);
        await ApplyAsync(collection, input);
        await _unitOfWork.SaveChangesAsync();
        return await GetAsync(collection.Id, null);
    }

    public async Task DeleteAsync(int id)
    {
        var collection = await _collectionRepository.GetAll().FirstOrDefaultAsync(c => c.Id == id)
                         ?? throw new EntityNotFoundException(NotFound);
        _collectionRepository.Delete(collection); // xóa mềm; liên kết sản phẩm giữ nguyên, không ảnh hưởng sản phẩm
        await _unitOfWork.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Hỗ trợ

    private async Task ApplyAsync(Collection c, CreateOrUpdateCollectionInput input)
    {
        var name = input.Name.Trim();
        var slug = Slugify(string.IsNullOrWhiteSpace(input.Slug) ? name : input.Slug);
        if (slug.Length == 0)
            throw new UserFriendlyException("Đường dẫn (slug) không hợp lệ.");
        if (await _collectionRepository.GetAll().AnyAsync(x => x.Slug == slug && x.Id != c.Id))
            throw new UserFriendlyException($"Đường dẫn \"{slug}\" đã được bộ sưu tập khác dùng.");

        c.Name = name;
        c.Slug = slug;
        c.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        c.CoverImage = string.IsNullOrWhiteSpace(input.CoverImage) ? null : input.CoverImage.Trim();
        c.IsActive = input.IsActive;
        c.DisplayOrder = input.DisplayOrder;

        // Danh sách sản phẩm: giữ đúng thứ tự gửi lên, bỏ trùng và mã không tồn tại.
        var ids = input.ProductIds.Distinct().ToList();
        var existing = await _productRepository.GetAll().Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        var valid = ids.Where(existing.Contains).ToList();
        foreach (var removed in c.Items.Where(ci => !valid.Contains(ci.ProductId)).ToList())
        {
            c.Items.Remove(removed);
            _itemRepository.Delete(removed);
        }
        // Sản phẩm đã có thì chỉ cập nhật thứ tự (tránh xóa rồi thêm lại vướng chỉ mục duy nhất CollectionId + ProductId).
        for (var i = 0; i < valid.Count; i++)
        {
            var item = c.Items.FirstOrDefault(ci => ci.ProductId == valid[i]);
            if (item is null)
                c.Items.Add(new CollectionProduct { ProductId = valid[i], DisplayOrder = i });
            else
                item.DisplayOrder = i;
        }
    }

    private IQueryable<CollectionDto> Project(IQueryable<Collection> query, bool manage) =>
        query.Select(c => new CollectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            CoverImage = c.CoverImage,
            IsActive = c.IsActive,
            DisplayOrder = c.DisplayOrder,
            CreationTime = c.CreationTime,
            ProductCount = c.Items.Count(ci => manage || ci.Product.IsActive),
            PreviewImages = c.Items
                .Where(ci => ci.Product.IsActive && ci.Product.MainImage != "")
                .OrderBy(ci => ci.DisplayOrder).Select(ci => ci.Product.MainImage).Take(4).ToList()
        });

    /// <summary>"Quà tặng Tết 2027" → "qua-tang-tet-2027".</summary>
    public static string Slugify(string text)
    {
        var normalized = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var ascii = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), "[^a-z0-9]+", "-").Trim('-');
        return ascii.Length > 120 ? ascii[..120].Trim('-') : ascii;
    }
}
