namespace HornViet.Application.Common;

/// <summary>
/// Có nhân viên / quản trị viên nào đang mở trang quản trị không (đang kết nối thời gian thực).
/// Cài đặt ở tầng Api bằng ConnectionTracker của SignalR.
/// </summary>
public interface IStaffPresence
{
    bool AnyStaffOnline();
}
