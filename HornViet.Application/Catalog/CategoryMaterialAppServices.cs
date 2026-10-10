using System.ComponentModel.DataAnnotations;
using HornViet.Application.Common;
using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Catalog;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    /// <summary>Số sản phẩm đang bán thuộc danh mục.</summary>
    public int ProductCount { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateOrUpdateCategoryInput
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên danh mục."), StringLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}

public interface ICategoryAppService
{
    Task<ListResultDto<CategoryDto>> GetAllAsync();
    Task<CategoryDto> GetAsync(int id);
    Task<CategoryDto> CreateAsync(CreateOrUpdateCategoryInput input);
    Task<CategoryDto> UpdateAsync(CreateOrUpdateCategoryInput input);
    Task DeleteAsync(int id);
}

public class CategoryAppService : ICategoryAppService
{
    private readonly IRepository<Category, int> _categoryRepository;
    private readonly IRepository<Product, int> _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryAppService(IRepository<Category, int> categoryRepository,
        IRepository<Product, int> productRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ListResultDto<CategoryDto>> GetAllAsync()
    {
        var items = await Project(_categoryRepository.GetAll().OrderBy(c => c.Name)).ToListAsync();
        return new ListResultDto<CategoryDto>(items);
    }

    public async Task<CategoryDto> GetAsync(int id) =>
        await Project(_categoryRepository.GetAll().Where(c => c.Id == id)).FirstOrDefaultAsync()
        ?? throw new EntityNotFoundException("Không tìm thấy danh mục.");

    public async Task<CategoryDto> CreateAsync(CreateOrUpdateCategoryInput input)
    {
        await EnsureUniqueNameAsync(input.Name, null);
        var category = new Category { Name = input.Name.Trim(), Description = input.Description };
        await _categoryRepository.InsertAsync(category);
        await _unitOfWork.SaveChangesAsync();
        return await GetAsync(category.Id);
    }

    public async Task<CategoryDto> UpdateAsync(CreateOrUpdateCategoryInput input)
    {
        if (input.Id is not { } id) throw new UserFriendlyException("Thiếu mã danh mục.");
        var category = await _categoryRepository.GetAll().FirstOrDefaultAsync(c => c.Id == id)
                       ?? throw new EntityNotFoundException("Không tìm thấy danh mục.");
        await EnsureUniqueNameAsync(input.Name, id);
        category.Name = input.Name.Trim();
        category.Description = input.Description;
        await _unitOfWork.SaveChangesAsync();
        return await GetAsync(id);
    }

    /// <summary>Xóa mềm. Từ chối nếu danh mục còn sản phẩm để sản phẩm không bị "mồ côi" danh mục.</summary>
    public async Task DeleteAsync(int id)
    {
        var category = await _categoryRepository.GetAll().FirstOrDefaultAsync(c => c.Id == id)
                       ?? throw new EntityNotFoundException("Không tìm thấy danh mục.");
        if (await _productRepository.GetAll().AnyAsync(p => p.CategoryId == id))
            throw new UserFriendlyException("Danh mục đang chứa sản phẩm. Hãy chuyển hoặc ẩn các sản phẩm trước khi xóa.");
        _categoryRepository.Delete(category);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureUniqueNameAsync(string name, int? exceptId)
    {
        var trimmed = name.Trim();
        if (await _categoryRepository.GetAll().AnyAsync(c => c.Name == trimmed && c.Id != exceptId))
            throw new UserFriendlyException($"Danh mục \"{trimmed}\" đã tồn tại. Vui lòng nhập tên khác.");
    }

    private IQueryable<CategoryDto> Project(IQueryable<Category> query)
    {
        var products = _productRepository.GetAll();
        return query.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            CreationTime = c.CreationTime,
            ProductCount = products.Count(p => p.CategoryId == c.Id && p.IsActive)
        });
    }
}


public class MaterialDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Origin { get; set; }
    public string? Description { get; set; }
    /// <summary>Ảnh minh họa (/uploads/...), null nếu chưa có.</summary>
    public string? Image { get; set; }
}

public class CreateOrUpdateMaterialInput
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên nguyên liệu."), StringLength(255)]
    public string Name { get; set; } = null!;

    [StringLength(255)]
    public string? Origin { get; set; }

    public string? Description { get; set; }

    /// <summary>Đường dẫn ảnh trả về từ /api/services/app/File/UploadImage; null / rỗng = không có ảnh.</summary>
    [StringLength(500)]
    public string? Image { get; set; }
}

public interface IMaterialAppService
{
    Task<ListResultDto<MaterialDto>> GetAllAsync();
    Task<MaterialDto> GetAsync(int id);
    Task<MaterialDto> CreateAsync(CreateOrUpdateMaterialInput input);
    Task<MaterialDto> UpdateAsync(CreateOrUpdateMaterialInput input);
    Task DeleteAsync(int id);
}

public class MaterialAppService : IMaterialAppService
{
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<Product, int> _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MaterialAppService(IRepository<Material, int> materialRepository,
        IRepository<Product, int> productRepository, IUnitOfWork unitOfWork)
    {
        _materialRepository = materialRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ListResultDto<MaterialDto>> GetAllAsync()
    {
        var items = await _materialRepository.GetAll().OrderBy(m => m.Name).Select(m => ToDto(m)).ToListAsync();
        return new ListResultDto<MaterialDto>(items);
    }

    public async Task<MaterialDto> GetAsync(int id) => ToDto(await FindAsync(id));

    public async Task<MaterialDto> CreateAsync(CreateOrUpdateMaterialInput input)
    {
        await EnsureUniqueNameAsync(input.Name, null);
        var material = new Material
        {
            Name = input.Name.Trim(), Origin = input.Origin, Description = input.Description, Image = NormalizeImage(input.Image)
        };
        await _materialRepository.InsertAsync(material);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(material);
    }

    public async Task<MaterialDto> UpdateAsync(CreateOrUpdateMaterialInput input)
    {
        if (input.Id is not { } id) throw new UserFriendlyException("Thiếu mã nguyên liệu.");
        var material = await FindAsync(id);
        await EnsureUniqueNameAsync(input.Name, id);
        material.Name = input.Name.Trim();
        material.Origin = input.Origin;
        material.Description = input.Description;
        material.Image = NormalizeImage(input.Image);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(material);
    }

    public async Task DeleteAsync(int id)
    {
        var material = await FindAsync(id);
        if (await _productRepository.GetAll().AnyAsync(p => p.MaterialId == id))
            throw new UserFriendlyException("Nguyên liệu đang được dùng cho sản phẩm, không thể xóa.");
        _materialRepository.Delete(material);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Material> FindAsync(int id) =>
        await _materialRepository.GetAll().FirstOrDefaultAsync(m => m.Id == id)
        ?? throw new EntityNotFoundException("Không tìm thấy nguyên liệu.");

    private async Task EnsureUniqueNameAsync(string name, int? exceptId)
    {
        var trimmed = name.Trim();
        if (await _materialRepository.GetAll().AnyAsync(m => m.Name == trimmed && m.Id != exceptId))
            throw new UserFriendlyException($"Nguyên liệu \"{trimmed}\" đã tồn tại.");
    }

    private static string? NormalizeImage(string? image) => string.IsNullOrWhiteSpace(image) ? null : image.Trim();

    private static MaterialDto ToDto(Material m) =>
        new() { Id = m.Id, Name = m.Name, Origin = m.Origin, Description = m.Description, Image = m.Image };
}
