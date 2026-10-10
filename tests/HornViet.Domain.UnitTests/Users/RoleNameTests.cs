using HornViet.Domain.Users.Enums;

namespace HornViet.Domain.UnitTests.Users;

public class RoleNameTests
{
    // Tên vai trò được lưu dạng chuỗi ở AbpRoles.Name, claim Role trong JWT và được frontend so sánh:
    // đổi tên giá trị enum sẽ làm lệch dữ liệu nên test này giữ cố định đúng ba tên.
    [Fact]
    public void RoleNames_MatchStoredStrings()
    {
        Assert.Equal(new[] { "Admin", "Staff", "Customer" }, Enum.GetNames<RoleName>());
    }
}
