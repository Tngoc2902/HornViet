using Website.Application.Common;
using Website.Domain;
using Website.Domain.Catalog.Entities;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.Orders.Entities;
using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Website.Application.Statistics.Enums;
using Website.Domain.Orders.Enums;
using Website.Domain.Users.Enums;

namespace Website.Application.Statistics;

public class GetRevenueInput
{
    /// <summary>Mặc định: 30 ngày gần nhất.</summary>
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public RevenueGroupBy GroupBy { get; set; } = RevenueGroupBy.Day;
}

public class RevenuePointDto
{
    /// <summary>Ngày đầu kỳ (theo ngày hoặc ngày 1 của tháng).</summary>
    public DateTime Period { get; set; }
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class RevenueReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }
    public IReadOnlyList<RevenuePointDto> Items { get; set; } = Array.Empty<RevenuePointDto>();
}

public class GetTopProductsInput
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Top { get; set; } = 10;
}

public class TopProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class LowStockVariantDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public int VariantId { get; set; }
    public string VariantName { get; set; } = null!;
    public int StockQuantity { get; set; }
}

public class DashboardDto
{
    public int PendingOrderCount { get; set; }
    public int ShippingOrderCount { get; set; }
    public int TodayOrderCount { get; set; }
    public int CustomerCount { get; set; }
    /// <summary>Doanh thu chỉ trả về cho Quản trị viên (null với Nhân viên).</summary>
    public decimal? TodayRevenue { get; set; }
    public decimal? MonthRevenue { get; set; }
    public IReadOnlyList<LowStockVariantDto> LowStockVariants { get; set; } = Array.Empty<LowStockVariantDto>();
}

public interface IStatisticAppService
{
    Task<RevenueReportDto> GetRevenueAsync(GetRevenueInput input);
    Task<ListResultDto<TopProductDto>> GetTopProductsAsync(GetTopProductsInput input);
    Task<DashboardDto> GetDashboardAsync();
}

/// <summary>Doanh thu chỉ tính trên đơn ở trạng thái Hoàn thành, theo ngày đặt hàng.</summary>
public class StatisticAppService : IStatisticAppService
{
    private readonly IRepository<Order, int> _orderRepository;
    private readonly IRepository<OrderItem, int> _orderItemRepository;
    private readonly IRepository<ProductVariant, int> _variantRepository;
    private readonly IRepository<User, long> _userRepository;
    private readonly IAppSession _session;

    public StatisticAppService(IRepository<Order, int> orderRepository, IRepository<OrderItem, int> orderItemRepository,
        IRepository<ProductVariant, int> variantRepository, IRepository<User, long> userRepository, IAppSession session)
    {
        _orderRepository = orderRepository;
        _orderItemRepository = orderItemRepository;
        _variantRepository = variantRepository;
        _userRepository = userRepository;
        _session = session;
    }

    public async Task<RevenueReportDto> GetRevenueAsync(GetRevenueInput input)
    {
        var (from, toExclusive) = ResolveRange(input.FromDate, input.ToDate);
        var completed = CompletedOrders(from, toExclusive);

        List<RevenuePointDto> items;
        if (input.GroupBy == RevenueGroupBy.Month)
        {
            var rows = await completed
                .GroupBy(o => new { o.CreationTime.Year, o.CreationTime.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync();
            items = rows.Select(r => new RevenuePointDto
                { Period = new DateTime(r.Year, r.Month, 1, 0, 0, 0, DateTimeKind.Local), Revenue = r.Revenue, OrderCount = r.Count }).ToList();
        }
        else
        {
            items = await completed
                .GroupBy(o => o.CreationTime.Date)
                .Select(g => new RevenuePointDto { Period = g.Key, Revenue = g.Sum(o => o.TotalAmount), OrderCount = g.Count() })
                .ToListAsync();
        }

        items = items.OrderBy(i => i.Period).ToList();
        return new RevenueReportDto
        {
            FromDate = from,
            ToDate = toExclusive.AddDays(-1),
            TotalRevenue = items.Sum(i => i.Revenue),
            OrderCount = items.Sum(i => i.OrderCount),
            Items = items
        };
    }

    public async Task<ListResultDto<TopProductDto>> GetTopProductsAsync(GetTopProductsInput input)
    {
        var (from, toExclusive) = ResolveRange(input.FromDate, input.ToDate);
        var top = Math.Clamp(input.Top, 1, 50);

        var orders = CompletedOrders(from, toExclusive);
        var variants = _variantRepository.GetAll().IgnoreQueryFilters();

        var rows = await (
                from oi in _orderItemRepository.GetAll()
                join o in orders on oi.OrderId equals o.Id
                join v in variants on oi.ProductVariantId equals v.Id
                group oi by v.ProductId
                into g
                select new TopProductDto
                {
                    ProductId = g.Key,
                    ProductName = g.Max(x => x.ProductName)!,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.UnitPrice * x.Quantity)
                })
            .OrderByDescending(x => x.QuantitySold).ThenByDescending(x => x.Revenue)
            .Take(top)
            .ToListAsync();

        return new ListResultDto<TopProductDto>(rows);
    }

    /// <summary>Trang tổng quan cho Nhân viên và Quản trị viên (Hình 4.7).</summary>
    public async Task<DashboardDto> GetDashboardAsync()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var orders = _orderRepository.GetAll();

        var dto = new DashboardDto
        {
            PendingOrderCount = await orders.CountAsync(o => o.Status == OrderStatus.Pending),
            ShippingOrderCount = await orders.CountAsync(o => o.Status == OrderStatus.Shipping),
            TodayOrderCount = await orders.CountAsync(o => o.CreationTime >= today),
            CustomerCount = await _userRepository.GetAll().CountAsync(u => u.Roles.Any(r => r.Role.Name == nameof(RoleName.Customer))),
            LowStockVariants = await _variantRepository.GetAll()
                .Where(v => v.StockQuantity <= AppConsts.LowStockThreshold && v.Product.IsActive)
                .OrderBy(v => v.StockQuantity).ThenBy(v => v.Id)
                .Take(10)
                .Select(v => new LowStockVariantDto
                {
                    ProductId = v.ProductId,
                    ProductName = v.Product.Name,
                    VariantId = v.Id,
                    // DisplayName không dịch được sang SQL nên ghép tại đây.
                    VariantName = (v.Size ?? "") + (v.Size != null && v.Style != null ? " / " : "") + (v.Style ?? ""),
                    StockQuantity = v.StockQuantity
                })
                .ToListAsync()
        };

        if (_session.IsInRole(RoleName.Admin))
        {
            dto.TodayRevenue = await CompletedOrders(today, today.AddDays(1)).SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            dto.MonthRevenue = await CompletedOrders(monthStart, monthStart.AddMonths(1)).SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
        }
        return dto;
    }

    private IQueryable<Order> CompletedOrders(DateTime from, DateTime toExclusive) =>
        _orderRepository.GetAll().Where(o =>
            o.Status == OrderStatus.Completed && o.CreationTime >= from && o.CreationTime < toExclusive);

    private static (DateTime From, DateTime ToExclusive) ResolveRange(DateTime? fromDate, DateTime? toDate)
    {
        var to = DateTime.SpecifyKind((toDate ?? DateTime.Today).Date, DateTimeKind.Local);
        var from = DateTime.SpecifyKind((fromDate ?? to.AddDays(-29)).Date, DateTimeKind.Local);
        if (from > to)
            throw new UserFriendlyException("Ngày bắt đầu phải trước ngày kết thúc.");
        return (from, to.AddDays(1));
    }
}
