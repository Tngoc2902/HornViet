using System.ComponentModel.DataAnnotations;
using HornViet.Application.Common;
using HornViet.Domain;
using HornViet.Domain.Carts.Entities;
using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.Orders.Entities;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using HornViet.Domain.Orders.Enums;

namespace HornViet.Application.Orders;

public class CreateOrderInput
{
    [Required(ErrorMessage = "Vui lòng nhập tên người nhận."), StringLength(255)]
    public string ReceiverName { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(AppConsts.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ nhận hàng."), StringLength(500)]
    public string ShippingAddress { get; set; } = null!;

    [EnumDataType(typeof(PaymentMethod), ErrorMessage = "Phương thức thanh toán không hợp lệ.")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;

    [StringLength(500)]
    public string? Note { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductName { get; set; } = null!;
    public string VariantName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public DateTime CreationTime { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public string ReceiverName { get; set; } = null!;
    public bool CanCancel { get; set; }
    /// <summary>Các sản phẩm trong đơn (tên, biến thể, số lượng) để danh sách đơn hiện được đơn bán gì.</summary>
    public List<OrderLineSummaryDto> Items { get; set; } = [];
}

public class OrderLineSummaryDto
{
    public string ProductName { get; set; } = null!;
    public string VariantName { get; set; } = null!;
    public int Quantity { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public DateTime CreationTime { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public bool CanCancel { get; set; }
    public string ReceiverName { get; set; } = null!;
    public long UserId { get; set; }
    /// <summary>Tên tài khoản đặt hàng — chỉ trả về cho Nhân viên/Quản trị viên.</summary>
    public string? UserName { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public string ShippingAddress { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? LastStatusChangeTime { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; set; } = Array.Empty<OrderItemDto>();
}

public class GetMyOrdersInput : PagedInput
{
    public OrderStatus? Status { get; set; }
}

public class GetOrdersInput : PagedInput
{
    public OrderStatus? Status { get; set; }

    /// <summary>Tìm theo mã đơn (HSV000123), tên người nhận hoặc số điện thoại.</summary>
    public string? Keyword { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class UpdateOrderStatusInput
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus Status { get; set; }
}

public interface IOrderAppService
{
    Task<OrderDto> CreateAsync(CreateOrderInput input);
    Task<PagedResultDto<OrderListItemDto>> GetMyOrdersAsync(GetMyOrdersInput input);
    Task<OrderDto> GetAsync(int id);
    Task<OrderDto> CancelAsync(int id);
    Task<PagedResultDto<OrderListItemDto>> GetAllAsync(GetOrdersInput input);
    Task<OrderDto> UpdateStatusAsync(UpdateOrderStatusInput input);
}

public class OrderAppService : IOrderAppService
{
    private readonly IRepository<Order, int> _orderRepository;
    private readonly IRepository<CartItem, int> _cartItemRepository;
    private readonly IRepository<Cart, int> _cartRepository;
    private readonly IRepository<ProductVariant, int> _variantRepository;
    private readonly IRepository<User, long> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;

    public OrderAppService(
        IRepository<Order, int> orderRepository,
        IRepository<CartItem, int> cartItemRepository,
        IRepository<Cart, int> cartRepository,
        IRepository<ProductVariant, int> variantRepository,
        IRepository<User, long> userRepository,
        IUnitOfWork unitOfWork,
        IAppSession session)
    {
        _orderRepository = orderRepository;
        _cartItemRepository = cartItemRepository;
        _cartRepository = cartRepository;
        _variantRepository = variantRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    // ------------------------------------------------------------------ Khách hàng

    /// <summary>
    /// Đặt hàng từ giỏ: kiểm tra tồn kho → tạo đơn (lưu đơn giá tại thời điểm mua) → trừ kho theo biến thể
    /// → làm trống giỏ. Tất cả nằm trong một lần SaveChanges (một giao dịch); rowversion trên biến thể
    /// đảm bảo hai đơn đồng thời không thể bán vượt tồn kho.
    /// </summary>
    public async Task<OrderDto> CreateAsync(CreateOrderInput input)
    {
        var userId = _session.GetUserId();
        var cartItems = await _cartItemRepository.GetAll()
            .Include(ci => ci.ProductVariant).ThenInclude(v => v.Product)
            .Where(ci => _cartRepository.GetAll().Any(c => c.Id == ci.CartId && c.UserId == userId))
            .OrderBy(ci => ci.Id)
            .ToListAsync();

        if (cartItems.Count == 0)
            throw new UserFriendlyException("Giỏ hàng đang trống.");

        // Gom mọi lỗi tồn kho để khách sửa giỏ một lần.
        var problems = cartItems
            .Where(ci => !ci.ProductVariant.Product.IsActive || ci.Quantity > ci.ProductVariant.StockQuantity)
            .Select(ci => !ci.ProductVariant.Product.IsActive
                ? $"{ci.ProductVariant.Product.Name} đã ngừng kinh doanh."
                : $"{ci.ProductVariant.Product.Name} ({ci.ProductVariant.DisplayName}) chỉ còn {ci.ProductVariant.StockQuantity} sản phẩm.")
            .ToList();
        if (problems.Count > 0)
            throw new UserFriendlyException("Một số sản phẩm trong giỏ không đủ hàng, vui lòng cập nhật lại giỏ.",
                string.Join("\n", problems));

        var order = new Order(userId, input.ReceiverName.Trim(), input.PhoneNumber.Trim(),
            input.ShippingAddress.Trim(), input.PaymentMethod, input.Note?.Trim());

        foreach (var item in cartItems)
        {
            var variant = item.ProductVariant;
            variant.DecreaseStock(item.Quantity);
            order.AddItem(variant.Id, variant.Product.Name, variant.DisplayName, item.Quantity, variant.Product.Price,
                variant.AverageCost); // giá vốn lúc bán → tính lợi nhuận (null nếu biến thể chưa có phiếu nhập)
            _cartItemRepository.Delete(item);
        }

        await _orderRepository.InsertAsync(order);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(order, null);
    }

    public async Task<PagedResultDto<OrderListItemDto>> GetMyOrdersAsync(GetMyOrdersInput input)
    {
        var userId = _session.GetUserId();
        var query = _orderRepository.GetAll().Where(o => o.UserId == userId);
        if (input.Status.HasValue)
            query = query.Where(o => o.Status == input.Status.Value);
        return await ToPagedListAsync(query, input);
    }

    /// <summary>Khách hàng chỉ xem được đơn của mình; Nhân viên/Quản trị viên xem mọi đơn.</summary>
    public async Task<OrderDto> GetAsync(int id)
    {
        var order = await LoadOrderAsync(id);
        var isManagement = _session.IsManagement();
        if (!isManagement && order.UserId != _session.GetUserId())
            throw new EntityNotFoundException("Không tìm thấy đơn hàng.");

        string? userName = null;
        if (isManagement)
            userName = await _userRepository.GetAll().IgnoreQueryFilters()
                .Where(u => u.Id == order.UserId).Select(u => u.UserName).FirstOrDefaultAsync();

        return ToDto(order, userName);
    }

    /// <summary>Khách hủy đơn còn ở trạng thái Chờ xử lý; tồn kho được hoàn lại.</summary>
    public async Task<OrderDto> CancelAsync(int id)
    {
        var order = await LoadOrderAsync(id);
        if (order.UserId != _session.GetUserId())
            throw new EntityNotFoundException("Không tìm thấy đơn hàng.");
        if (order.Status != OrderStatus.Pending)
            throw new UserFriendlyException("Đơn hàng đã được xử lý hoặc đang giao, không thể hủy.");

        order.ChangeStatus(OrderStatus.Canceled);
        await RestockAsync(order);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(order, null);
    }

    // ------------------------------------------------------------------ Quản trị

    public async Task<PagedResultDto<OrderListItemDto>> GetAllAsync(GetOrdersInput input)
    {
        var query = _orderRepository.GetAll();

        if (input.Status.HasValue)
            query = query.Where(o => o.Status == input.Status.Value);
        if (input.FromDate.HasValue)
            query = query.Where(o => o.CreationTime >= input.FromDate.Value.Date);
        if (input.ToDate.HasValue)
            query = query.Where(o => o.CreationTime < input.ToDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            var codeId = TryParseOrderCode(keyword);
            query = query.Where(o => o.Id == codeId || o.ReceiverName.Contains(keyword) || o.PhoneNumber.Contains(keyword));
        }

        return await ToPagedListAsync(query, input);
    }

    /// <summary>Chuyển trạng thái theo thứ tự Chờ xử lý → Đang giao → Hoàn thành, hoặc hủy (hoàn tồn kho).</summary>
    public async Task<OrderDto> UpdateStatusAsync(UpdateOrderStatusInput input)
    {
        var order = await LoadOrderAsync(input.Id);
        order.ChangeStatus(input.Status);
        if (input.Status == OrderStatus.Canceled)
            await RestockAsync(order);
        await _unitOfWork.SaveChangesAsync();
        return await GetAsync(order.Id);
    }

    // ------------------------------------------------------------------ Hỗ trợ

    private async Task RestockAsync(Order order)
    {
        var variantIds = order.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        // Bỏ bộ lọc xóa mềm: biến thể đã ngừng bán vẫn được cộng lại số lượng cho đúng sổ kho.
        var variants = await _variantRepository.GetAll().IgnoreQueryFilters()
            .Where(v => variantIds.Contains(v.Id)).ToListAsync();
        foreach (var item in order.Items)
            variants.First(v => v.Id == item.ProductVariantId).IncreaseStock(item.Quantity);
    }

    private async Task<Order> LoadOrderAsync(int id) =>
        await _orderRepository.GetAll().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id)
        ?? throw new EntityNotFoundException("Không tìm thấy đơn hàng.");

    private static async Task<PagedResultDto<OrderListItemDto>> ToPagedListAsync(IQueryable<Order> query, PagedInput input)
    {
        var totalCount = await query.CountAsync();
        var rows = await query
            .OrderByDescending(o => o.CreationTime).ThenByDescending(o => o.Id)
            .PageBy(input)
            .Select(o => new
            {
                o.Id, o.CreationTime, o.Status, o.PaymentMethod, o.TotalAmount, o.ReceiverName,
                ItemCount = o.Items.Sum(i => i.Quantity),
                Lines = o.Items.OrderBy(i => i.Id)
                    .Select(i => new OrderLineSummaryDto { ProductName = i.ProductName, VariantName = i.VariantName, Quantity = i.Quantity })
                    .ToList()
            })
            .ToListAsync();

        var items = rows.Select(o => new OrderListItemDto
        {
            Id = o.Id,
            Code = Order.FormatCode(o.Id),
            CreationTime = o.CreationTime,
            Status = o.Status,
            PaymentMethod = o.PaymentMethod,
            TotalAmount = o.TotalAmount,
            ItemCount = o.ItemCount,
            ReceiverName = o.ReceiverName,
            CanCancel = o.Status == OrderStatus.Pending,
            Items = o.Lines
        }).ToList();

        return new PagedResultDto<OrderListItemDto>(totalCount, items);
    }

    private static int TryParseOrderCode(string keyword)
    {
        var digits = keyword.StartsWith("HSV", StringComparison.OrdinalIgnoreCase) ? keyword[3..] : keyword;
        return int.TryParse(digits, out var id) ? id : -1;
    }

    private static OrderDto ToDto(Order o, string? userName) => new()
    {
        Id = o.Id,
        Code = Order.FormatCode(o.Id),
        CreationTime = o.CreationTime,
        Status = o.Status,
        PaymentMethod = o.PaymentMethod,
        TotalAmount = o.TotalAmount,
        ItemCount = o.Items.Sum(i => i.Quantity),
        ReceiverName = o.ReceiverName,
        CanCancel = o.Status == OrderStatus.Pending,
        UserId = o.UserId,
        UserName = userName,
        PhoneNumber = o.PhoneNumber,
        ShippingAddress = o.ShippingAddress,
        Note = o.Note,
        LastStatusChangeTime = o.LastStatusChangeTime,
        Items = o.Items.OrderBy(i => i.Id).Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductVariantId = i.ProductVariantId,
            ProductName = i.ProductName,
            VariantName = i.VariantName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineTotal = i.UnitPrice * i.Quantity
        }).ToList()
    };
}
