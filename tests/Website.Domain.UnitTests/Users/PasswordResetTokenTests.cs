using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;

namespace Website.Domain.UnitTests.Users;

public class PasswordResetTokenTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Local);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    [Fact]
    public void Issue_StoresOnlyHashOfRawToken()
    {
        var (token, raw) = PasswordResetToken.Issue(userId: 7, Now, Lifetime);

        Assert.Equal(64, raw.Length);
        Assert.NotEqual(raw, token.TokenHash);
        Assert.Equal(PasswordResetToken.Hash(raw), token.TokenHash);
        Assert.Equal(7, token.UserId);
        Assert.Equal(Now, token.CreationTime);
        Assert.Equal(Now.AddMinutes(30), token.ExpirationTime);
    }

    [Fact]
    public void Issue_GeneratesDifferentTokensEachTime()
    {
        var (_, first) = PasswordResetToken.Issue(1, Now, Lifetime);
        var (_, second) = PasswordResetToken.Issue(1, Now, Lifetime);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_IgnoresCaseAndSurroundingSpaces()
    {
        var (_, raw) = PasswordResetToken.Issue(1, Now, Lifetime);

        Assert.Equal(PasswordResetToken.Hash(raw), PasswordResetToken.Hash($"  {raw.ToLowerInvariant()} "));
    }

    [Fact]
    public void IsUsable_UntilExpiration()
    {
        var (token, _) = PasswordResetToken.Issue(1, Now, Lifetime);

        Assert.True(token.IsUsable(Now.AddMinutes(29)));
        Assert.False(token.IsUsable(Now.AddMinutes(30)));
    }

    [Fact]
    public void MarkUsed_CanOnlyBeUsedOnce()
    {
        var (token, _) = PasswordResetToken.Issue(1, Now, Lifetime);

        token.MarkUsed(Now.AddMinutes(1));

        Assert.False(token.IsUsable(Now.AddMinutes(2)));
        Assert.Throws<UserFriendlyException>(() => token.MarkUsed(Now.AddMinutes(2)));
    }

    [Fact]
    public void MarkUsed_ExpiredToken_Throws()
    {
        var (token, _) = PasswordResetToken.Issue(1, Now, Lifetime);

        var ex = Assert.Throws<UserFriendlyException>(() => token.MarkUsed(Now.AddMinutes(31)));
        Assert.Equal(PasswordResetToken.InvalidMessage, ex.Message);
    }

    [Fact]
    public void Revoke_MakesTokenUnusable_AndKeepsFirstTime()
    {
        var (token, _) = PasswordResetToken.Issue(1, Now, Lifetime);

        token.Revoke(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(5));

        Assert.False(token.IsUsable(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), token.UsedTime);
    }
}
