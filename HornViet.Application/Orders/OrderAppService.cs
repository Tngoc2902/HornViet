using System.ComponentModel.DataAnnotations;
using HornViet.Application.Common;
using HornViet.Application.Shipping;
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

    /// <summary>Giao tận nơi (mặc định) hay nhận tại cửa hàng.</summary>
    [EnumDataType(typeof(DeliveryMethod), ErrorMessage = "Hình thức nhận hàng không hợp lệ.")]
    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Shipping;

    /// <summary>
    /// Số nhà, tên đường — bắt buộc khi giao tận nơi, bỏ qua khi nhận tại cửa hàng. Khi bật vận chuyển, server ghép thêm
    /// phường / tỉnh (tên chuẩn của đơn vị vận chuyển) để thành địa chỉ đầy đủ.
    /// </summary>
    [StringLength(300)]
    public string? ShippingAddress { get; set; }

    /// <summary>Tỉnh / thành và phường / xã (danh mục mới 2 cấp của đơn vị vận chuyển) — bắt buộc khi đã bật vận chuyển.</summary>
    public int? ToProvinceId { get; set; }
    public string? ToWardCode { get; set; }

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
    public DeliveryMethod DeliveryMethod { get; set; }
    /// <summary>Tiền hàng (chưa gồm phí ship).</summary>
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    /// <summary>Số khách phải trả = tiền hàng + phí ship.</summary>
    public decimal PayableAmount { get; set; }
    public string? GhnOrderCode { get; set; }
    public string? GhnStatus { get; set; }
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
    public DeliveryMethod DeliveryMethod { get; set; }
    /// <summary>Tiền hàng (chưa gồm phí ship).</summary>
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    /// <summary>Số khách phải trả = tiền hàng + phí ship.</summary>
    public decimal PayableAmount { get; set; }
    public string? GhnOrderCode { get; set; }
    public string? GhnStatus { get; set; }
    public DateTime? GhnExpectedDeliveryTime { get; set; }
    public DateTime? GhnStatusTime { get; set; }
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

    /// <summary>Chỉ đơn giao tận nơi / chỉ đơn nhận tại cửa hàng; null = tất cả.</summary>
    public DeliveryMethod? DeliveryMethod { get; set; }

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

    /// <summary>
    /// Hệ thống áp trạng thái đơn theo kết quả giao hàng của đơn vị vận chuyển (giao thành công → Hoàn thành, hoàn hàng về → Hủy
    /// và hoàn tồn kho). Không kiểm tra quyền người dùng — chỉ gọi từ luồng đã xác thực của hệ thống (webhook / làm mới vận đơn).
    /// </summary>
    Task ApplyShipmentStatusAsync(int orderId, OrderStatus newStatus);
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
    private readonly IRealtimeNotifier _notifier;
    private readonly IShippingAppService _shipping;
    private readonly IShippingProvider _shippingProvider;

    public OrderAppService(
        IRepository<Order, int> orderRepository,
        IRepository<CartItem, int> cartItemRepository,
        IRepository<Cart, int> cartRepository,
        IRepository<ProductVariant, int> variantRepository,
        IRepository<User, long> userRepository,
        IUnitOfWork unitOfWork,
        IAppSession session,
        IRealtimeNotifier notifier,
        IShippingAppService shipping,
        IShippingProvider shippingProvider)
    {
        _orderRepository = orderRepository;
        _cartItemRepository = cartItemRepository;
        _cartRepository = cartRepository;
        _variantRepository = variantRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _session = session;
        _notifier = notifier;
        _shipping = shipping;
        _shippingProvider = shippingProvider;
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

        var pickup = input.DeliveryMethod == DeliveryMethod.StorePickup;
        var street = input.ShippingAddress?.Trim() ?? "";
        if (!pickup && street.Length == 0)
            throw new UserFriendlyException("Vui lòng nhập địa chỉ nhận hàng.");
        var address = pickup ? AppConsts.StorePickupAddress : street;
        ShippingQuote? quote = null;
        if (!pickup && _shipping.IsEnabled)
        {
            if (input.ToProvinceId is not > 0 || string.IsNullOrWhiteSpace(input.ToWardCode))
                throw new UserFriendlyException("Vui lòng chọn tỉnh / thành phố và phường / xã nhận hàng.");

            // Phí ship luôn do server tính lại — không nhận số tiền từ frontend.
            quote = await _shipping.QuoteAsync(input.ToProvinceId.Value, input.ToWardCode!,
                cartItems.Sum(ci => ci.Quantity), cartItems.Sum(ci => ci.ProductVariant.Product.Price * ci.Quantity));
            address = $"{street}, {quote.WardName}, {quote.ProvinceName}";
        }

        var order = new Order(userId, input.ReceiverName.Trim(), input.PhoneNumber.Trim(),
            address, input.PaymentMethod, input.Note?.Trim(), input.DeliveryMethod);
        if (quote != null)
            order.SetShipping(quote.Fee, input.ToProvinceId!.Value, input.ToWardCode!.Trim(), street);

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

        // Thời gian thực (sau khi đã lưu): báo quản trị có đơn mới, cập nhật tồn kho cho người đang xem sản phẩm.
        await _notifier.OrderCreatedAsync(new OrderCreatedEvent(order.Id, Order.FormatCode(order.Id),
            order.Items.Sum(i => i.Quantity), order.TotalAmount, order.CreationTime));
        await _notifier.StockChangedAsync(cartItems
            .Select(ci => new StockChange(ci.ProductVariant.ProductId, ci.ProductVariant.Id, ci.ProductVariant.StockQuantity,
                ci.ProductVariant.Product.Name, ci.ProductVariant.DisplayName))
            .ToList(), alertLowStock: true);
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
        // Đơn đã tự lên GHN lúc đặt: hủy cả vận đơn (GHN từ chối vì shipper đã lấy hàng thì không hủy đơn).
        if (!string.IsNullOrEmpty(order.GhnOrderCode))
        {
            await _shippingProvider.CancelShipmentAsync(order.GhnOrderCode, $"Khách hủy đơn {Order.FormatCode(order.Id)}");
            order.DetachShipment();
        }
        var restocked = await RestockAsync(order);
        await _unitOfWork.SaveChangesAsync();
        await NotifyStatusChangedAsync(order, restocked);
        return ToDto(order, null);
    }

    // ------------------------------------------------------------------ Quản trị

    public async Task<PagedResultDto<OrderListItemDto>> GetAllAsync(GetOrdersInput input)
    {
        var query = _orderRepository.GetAll();

        if (input.Status.HasValue)
            query = query.Where(o => o.Status == input.Status.Value);
        if (input.DeliveryMethod.HasValue)
            query = query.Where(o => o.DeliveryMethod == input.DeliveryMethod.Value);
        if (input.FromDate.HasValue)
            query = query.Where(o => o.CreationTime >= input.FromDate.Value.Date);
        if (input.ToDate.HasValue)
            query = query.Where(o => o.CreationTime < input.ToDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            var codeId = TryParseOrderCode(keyword);
            query = query.Where(o => o.Id == codeId || SearchText.Fold(o.ReceiverName).Contains(SearchText.Fold(keyword)) || o.PhoneNumber.Contains(keyword));
        }

        return await ToPagedListAsync(query, input);
    }

    /// <summary>
    /// Chuyển trạng thái theo thứ tự Chờ xử lý → Đang giao → Hoàn thành (nhận tại cửa hàng: Chờ xử lý → Chờ khách nhận → Hoàn thành),
    /// hoặc hủy (hoàn tồn kho).
    /// </summary>
    public async Task<OrderDto> UpdateStatusAsync(UpdateOrderStatusInput input)
    {
        var order = await LoadOrderAsync(input.Id);
        if (!string.IsNullOrEmpty(order.GhnOrderCode) && input.Status != OrderStatus.Canceled)
            throw new UserFriendlyException("Đơn đã có vận đơn Giao Hàng Nhanh: trạng thái tự cập nhật theo đơn vị vận chuyển.");
        order.ChangeStatus(input.Status); // kiểm tra chuyển trạng thái hợp lệ trước khi gọi GHN
        if (input.Status == OrderStatus.Canceled && !string.IsNullOrEmpty(order.GhnOrderCode))
        {
            await _shippingProvider.CancelShipmentAsync(order.GhnOrderCode, $"Hủy đơn {Order.FormatCode(order.Id)}");
            order.DetachShipment();
        }
        var restocked = input.Status == OrderStatus.Canceled ? await RestockAsync(order) : [];
        await _unitOfWork.SaveChangesAsync();
        await NotifyStatusChangedAsync(order, restocked);
        return await GetAsync(order.Id);
    }

    public async Task ApplyShipmentStatusAsync(int orderId, OrderStatus newStatus)
    {
        var order = await LoadOrderAsync(orderId);
        if (order.Status == newStatus)
        {
            await _unitOfWork.SaveChangesAsync(); // vẫn lưu các cột vận đơn đã cập nhật trên cùng đơn
            return;
        }

        // Giao thành công mà đơn còn "Chờ xử lý" (nhân viên chưa bấm Đang giao): đi qua Đang giao cho đúng thứ tự.
        if (order.Status == OrderStatus.Pending && newStatus == OrderStatus.Completed)
            order.ChangeStatus(OrderStatus.Shipping);
        order.ChangeStatus(newStatus);
        var restocked = newStatus == OrderStatus.Canceled ? await RestockAsync(order) : [];
        await _unitOfWork.SaveChangesAsync();
        await NotifyStatusChangedAsync(order, restocked);
    }

    // ------------------------------------------------------------------ Hỗ trợ

    /// <returns>Tồn kho mới của các biến thể được hoàn (để báo thời gian thực sau khi lưu).</returns>
    private async Task<List<StockChange>> RestockAsync(Order order)
    {
        var variantIds = order.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        // Bỏ bộ lọc xóa mềm: biến thể đã ngừng bán vẫn được cộng lại số lượng cho đúng sổ kho.
        var variants = await _variantRepository.GetAll().IgnoreQueryFilters()
            .Where(v => variantIds.Contains(v.Id)).ToListAsync();
        foreach (var item in order.Items)
            variants.First(v => v.Id == item.ProductVariantId).IncreaseStock(item.Quantity);

        return order.Items
            .GroupBy(i => i.ProductVariantId)
            .Select(g =>
            {
                var v = variants.First(x => x.Id == g.Key);
                return new StockChange(v.ProductId, v.Id, v.StockQuantity, g.First().ProductName, g.First().VariantName);
            })
            .ToList();
    }

    private async Task NotifyStatusChangedAsync(Order order, IReadOnlyCollection<StockChange> restocked)
    {
        await _notifier.OrderStatusChangedAsync(
            new OrderStatusChangedEvent(order.Id, Order.FormatCode(order.Id), order.UserId, order.Status));
        if (restocked.Count > 0)
            await _notifier.StockChangedAsync(restocked);
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
                o.Id, o.CreationTime, o.Status, o.PaymentMethod, o.DeliveryMethod, o.TotalAmount, o.ShippingFee, o.GhnOrderCode, o.GhnStatus, o.ReceiverName,
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
            DeliveryMethod = o.DeliveryMethod,
            TotalAmount = o.TotalAmount,
            ShippingFee = o.ShippingFee,
            PayableAmount = o.TotalAmount + o.ShippingFee,
            GhnOrderCode = o.GhnOrderCode,
            GhnStatus = o.GhnStatus,
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
        DeliveryMethod = o.DeliveryMethod,
        TotalAmount = o.TotalAmount,
        ShippingFee = o.ShippingFee,
        PayableAmount = o.PayableAmount,
        GhnOrderCode = o.GhnOrderCode,
        GhnStatus = o.GhnStatus,
        GhnExpectedDeliveryTime = o.GhnExpectedDeliveryTime,
        GhnStatusTime = o.GhnStatusTime,
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
