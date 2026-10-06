using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;

namespace Website.Application.Common;

/// <summary>Thông tin phiên đăng nhập hiện tại (tương đương AbpSession). Cài đặt tại Website.Api từ JWT.</summary>
public interface IAppSession
{
    long? UserId { get; }

    bool IsInRole(string roleName);
}

public static class AppSessionExtensions
{
    public static long GetUserId(this IAppSession session) =>
        session.UserId ?? throw new UserFriendlyException("Bạn cần đăng nhập để thực hiện thao tác này.");

    /// <summary>Nhân viên hoặc Quản trị viên.</summary>
    public static bool IsManagement(this IAppSession session) =>
        session.IsInRole(StaticRoleNames.Admin) || session.IsInRole(StaticRoleNames.Staff);
}
