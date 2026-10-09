using System.ComponentModel.DataAnnotations;
using Website.Application.Common;
using Website.Domain.Catalog.Entities;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.Inventory.Entities;
using Website.Domain.Orders.Entities;
using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace Website.Application.Inventory;

/// <summary>Một phiếu nhập kho trả về frontend.</summary>
public class StockReceiptDto
{
    public int Id { get; set; }
    public DateTime CreationTime { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public string VariantName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Note { get; set; }
    public string? CreatorUserName { get; set; }
    public DateTime? LastModificationTime { get; set; }
    /// <summary>true = phiếu mới nhất của biến thể → được sửa / hủy.</summary>
    public bool CanEdit { get; set; }
}

/// <summary>Tồn kho và giá vốn hiện tại của một biến thể.</summary>
public class VariantCostDto
{
    public int ProductVariantId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string VariantName { get; set; } = null!;
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    /// <summary>Giá vốn bình quân hiện tại; null = chưa có phiếu nhập.</summary>
    public decimal? AverageCost { get; set; }
    /// <summary>Giá trị tồn kho = tồn × giá vốn bình quân.</summary>
    public decimal? StockValue { get; set; }
    /// <summary>Lãi trên một sản phẩm nếu bán với giá hiện tại = giá bán − giá vốn bình quân.</summary>
    public decimal? UnitProfit { get; set; }
    public DateTime? LastReceiptTime { get; set; }
}

public class GetStockReceiptsInput : PagedInput
{
    public int? ProductVariantId { get; set; }
    /// <summary>Tìm theo tên sản phẩm.</summary>
    public string? Keyword { get; set; }
}

public class GetVariantCostsInput : PagedInput
{
    public string? Keyword { get; set; }
}

public class CreateStockReceiptInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn biến thể.")]
    public int ProductVariantId { get; set; }

    [Range(1, StockReceipt.MaxQuantity, ErrorMessage = "Số lượng nhập phải từ 1 đến 1.000.000.")]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Giá nhập không hợp lệ.")]
    public decimal UnitCost { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class UpdateStockReceiptInput
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Range(1, StockReceipt.MaxQuantity, ErrorMessage = "Số lượng nhập phải từ 1 đến 1.000.000.")]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Giá nhập không hợp lệ.")]
    public decimal UnitCost { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>UC Nhập kho &amp; giá vốn — chỉ Quản trị viên (controller gắn AdminOnly).</summary>
public interface IStockReceiptAppService
{
    Task<PagedResultDto<StockReceiptDto>> GetAllAsync(GetStockReceiptsInput input);
    Task<PagedResultDto<VariantCostDto>> GetVariantCostsAsync(GetVariantCostsInput input);
    Task<StockReceiptDto> CreateAsync(CreateStockReceiptInput input);
    Task<StockReceiptDto> UpdateAsync(UpdateStockReceiptInput input);
    Task DeleteAsync(int id);
}

public class StockReceiptAppService : IStockReceiptAppService
{
    private const string NotLatestMessage =
        "Chỉ sửa / hủy được phiếu nhập mới nhất của biến thể này (đã có phiếu nhập sau phiếu này).";

    private readonly IRepository<StockReceipt, int> _receiptRepository;
    private readonly IRepository<ProductVariant, int> _variantRepository;
    private readonly IRepository<Order, int> _orderRepository;
    private readonly IRepository<OrderItem, int> _orderItemRepository;
    private readonly IRepository<User, long> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;

    public StockReceiptAppService(IRepository<StockReceipt, int> receiptRepository, IRepository<ProductVariant, int> variantRepository,
        IRepository<Order, int> orderRepository, IRepository<OrderItem, int> orderItemRepository,
        IRepository<User, long> userRepository, IUnitOfWork unitOfWork, IAppSession session)
    {
        _receiptRepository = receiptRepository;
        _variantRepository = variantRepository;
        _orderRepository = orderRepository;
        _orderItemRepository = orderItemRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    public async Task<PagedResultDto<StockReceiptDto>> GetAllAsync(GetStockReceiptsInput input)
    {
        var receipts = _receiptRepository.GetAll();
        var query = receipts;
        if (input.ProductVariantId.HasValue)
            query = query.Where(r => r.ProductVariantId == input.ProductVariantId.Value);
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            query = query.Where(r => r.ProductVariant.Product.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync();
        var rows = await Project(query.OrderByDescending(r => r.Id).PageBy(input)).ToListAsync();
        var items = rows.Select(ToDto).ToList();
        return new PagedResultDto<StockReceiptDto>(totalCount, items);
    }

    public async Task<PagedResultDto<VariantCostDto>> GetVariantCostsAsync(GetVariantCostsInput input)
    {
        var query = _variantRepository.GetAll();
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            query = query.Where(v => v.Product.Name.Contains(keyword));
        }

        var receipts = _receiptRepository.GetAll();
        var totalCount = await query.CountAsync();
        var rows = await query
            .OrderBy(v => v.Product.Name).ThenBy(v => v.Id)
            .PageBy(input)
            .Select(v => new
            {
                v.Id, v.ProductId, ProductName = v.Product.Name, v.Size, v.Style, SalePrice = v.Product.Price,
                v.StockQuantity, v.AverageCost,
                LastReceiptTime = receipts.Where(r => r.ProductVariantId == v.Id).Max(r => (DateTime?)r.CreationTime)
            })
            .ToListAsync();

        var items = rows.Select(v => new VariantCostDto
        {
            ProductVariantId = v.Id,
            ProductId = v.ProductId,
            ProductName = v.ProductName,
            VariantName = VariantName(v.Size, v.Style),
            SalePrice = v.SalePrice,
            StockQuantity = v.StockQuantity,
            AverageCost = v.AverageCost,
            StockValue = v.AverageCost * v.StockQuantity,
            UnitProfit = v.SalePrice - v.AverageCost,
            LastReceiptTime = v.LastReceiptTime
        }).ToList();
        return new PagedResultDto<VariantCostDto>(totalCount, items);
    }

    public async Task<StockReceiptDto> CreateAsync(CreateStockReceiptInput input)
    {
        var variant = await _variantRepository.GetAll()
                          .Include(v => v.Product)
                          .FirstOrDefaultAsync(v => v.Id == input.ProductVariantId)
                      ?? throw new EntityNotFoundException("Biến thể sản phẩm không tồn tại.");

        var receipt = StockReceipt.Receive(variant, input.Quantity, input.UnitCost, Clean(input.Note), _session.GetUserId());
        await _receiptRepository.InsertAsync(receipt);
        await _unitOfWork.SaveChangesAsync();
        return await GetDtoAsync(receipt.Id);
    }

    /// <summary>
    /// Sửa phiếu nhập mới nhất: tồn kho và giá vốn bình quân của biến thể được tính lại, đồng thời giá vốn của các đơn
    /// đã bán biến thể này SAU phiếu nhập được cập nhật theo giá đúng → báo cáo lợi nhuận tự đúng.
    /// </summary>
    public async Task<StockReceiptDto> UpdateAsync(UpdateStockReceiptInput input)
    {
        var receipt = await LoadLatestAsync(input.Id);
        receipt.Correct(input.Quantity, input.UnitCost, Clean(input.Note), _session.GetUserId(), DateTime.Now);
        await RecostSalesAfterAsync(receipt);
        await _unitOfWork.SaveChangesAsync();
        return await GetDtoAsync(receipt.Id);
    }

    /// <summary>Hủy phiếu nhập mới nhất: trừ lại số lượng, giá vốn trở về như trước khi nhập.</summary>
    public async Task DeleteAsync(int id)
    {
        var receipt = await LoadLatestAsync(id);
        receipt.Revert();
        await RecostSalesAfterAsync(receipt);
        _receiptRepository.Delete(receipt);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<StockReceipt> LoadLatestAsync(int id)
    {
        var receipt = await _receiptRepository.GetAll()
                          .Include(r => r.ProductVariant)
                          .FirstOrDefaultAsync(r => r.Id == id)
                      ?? throw new EntityNotFoundException("Không tìm thấy phiếu nhập.");
        if (receipt.ProductVariant == null)
            throw new UserFriendlyException("Biến thể của phiếu nhập này đã bị xóa, không thể sửa.");
        if (await _receiptRepository.GetAll().AnyAsync(r => r.ProductVariantId == receipt.ProductVariantId && r.Id > receipt.Id))
            throw new UserFriendlyException(NotLatestMessage);
        return receipt;
    }

    /// <summary>Các dòng đơn bán biến thể này sau thời điểm nhập mang giá vốn bình quân mới.</summary>
    private async Task RecostSalesAfterAsync(StockReceipt receipt)
    {
        var orders = _orderRepository.GetAll();
        var lines = await _orderItemRepository.GetAll()
            .Where(oi => oi.ProductVariantId == receipt.ProductVariantId &&
                         orders.Any(o => o.Id == oi.OrderId && o.CreationTime >= receipt.CreationTime))
            .ToListAsync();
        foreach (var line in lines)
            line.UnitCost = receipt.ProductVariant.AverageCost;
    }

    private async Task<StockReceiptDto> GetDtoAsync(int id) =>
        ToDto(await Project(_receiptRepository.GetAll().Where(r => r.Id == id)).FirstOrDefaultAsync()
              ?? throw new EntityNotFoundException("Không tìm thấy phiếu nhập."));

    /// <summary>Chiếu phiếu nhập sang dòng dữ liệu (một câu SQL): tên sản phẩm, người tạo, có phải phiếu mới nhất không.</summary>
    private IQueryable<ReceiptRow> Project(IQueryable<StockReceipt> query)
    {
        var receipts = _receiptRepository.GetAll();
        var users = _userRepository.GetAll().IgnoreQueryFilters();
        return query.Select(r => new ReceiptRow
        {
            Id = r.Id, CreationTime = r.CreationTime, ProductId = r.ProductVariant.ProductId,
            ProductName = r.ProductVariant.Product.Name, ProductVariantId = r.ProductVariantId,
            Size = r.ProductVariant.Size, Style = r.ProductVariant.Style, Quantity = r.Quantity, UnitCost = r.UnitCost,
            Note = r.Note, LastModificationTime = r.LastModificationTime,
            CreatorUserName = users.Where(u => u.Id == r.CreatorUserId).Select(u => u.UserName).FirstOrDefault(),
            IsLatest = !receipts.Any(x => x.ProductVariantId == r.ProductVariantId && x.Id > r.Id)
        });
    }

    private static StockReceiptDto ToDto(ReceiptRow r) => new()
    {
        Id = r.Id, CreationTime = r.CreationTime, ProductId = r.ProductId, ProductName = r.ProductName,
        ProductVariantId = r.ProductVariantId, VariantName = VariantName(r.Size, r.Style), Quantity = r.Quantity,
        UnitCost = r.UnitCost, TotalCost = r.UnitCost * r.Quantity, Note = r.Note, CreatorUserName = r.CreatorUserName,
        LastModificationTime = r.LastModificationTime, CanEdit = r.IsLatest
    };

    private sealed class ReceiptRow
    {
        public int Id { get; init; }
        public DateTime CreationTime { get; init; }
        public int ProductId { get; init; }
        public string ProductName { get; init; } = null!;
        public int ProductVariantId { get; init; }
        public string? Size { get; init; }
        public string? Style { get; init; }
        public int Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public string? Note { get; init; }
        public DateTime? LastModificationTime { get; init; }
        public string? CreatorUserName { get; init; }
        public bool IsLatest { get; init; }
    }

    private static string VariantName(string? size, string? style)
    {
        var name = string.Join(" / ", new[] { size, style }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return name.Length == 0 ? "Mặc định" : name;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
