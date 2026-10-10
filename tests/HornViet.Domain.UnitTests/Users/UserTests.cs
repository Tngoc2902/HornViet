using HornViet.Domain.Users.Entities;

namespace HornViet.Domain.UnitTests.Users;

public class UserTests
{
    [Fact]
    public void NewUser_HasSecurityStamp()
    {
        var user = new User();

        Assert.Equal(32, user.SecurityStamp.Length);
        Assert.NotEqual(new User().SecurityStamp, user.SecurityStamp);
    }

    [Fact]
    public void RotateSecurityStamp_ChangesStamp()
    {
        var user = new User();
        var before = user.SecurityStamp;

        user.RotateSecurityStamp();

        Assert.NotEqual(before, user.SecurityStamp);
        Assert.Equal(32, user.SecurityStamp.Length);
    }
}
