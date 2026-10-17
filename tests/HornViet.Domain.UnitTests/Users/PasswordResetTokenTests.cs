using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Entities;

namespace HornViet.Domain.UnitTests.Users;

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
        Assert.Equal(PasswordResetToken.InvalidTicketMessage, ex.Message); // Issue() cấp vé
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

    // ---- Mã xác nhận 6 số (UC Quên mật khẩu bằng mã)

    [Fact]
    public void IssueCode_Creates6DigitCode_StoresOnlySaltedHash_AndExpiresIn60Seconds()
    {
        var (token, code) = PasswordResetToken.IssueCode(userId: 7, Now, TimeSpan.FromSeconds(60));

        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(PasswordResetPurpose.Code, token.Purpose);
        Assert.Equal(PasswordResetToken.HashCode(7, code), token.TokenHash);
        Assert.NotEqual(PasswordResetToken.HashCode(8, code), token.TokenHash); // cùng mã, khác người → khác bản băm
        Assert.True(token.IsUsable(Now.AddSeconds(59)));
        Assert.False(token.IsUsable(Now.AddSeconds(60)));
    }

    [Fact]
    public void Issue_CreatesTicket()
    {
        var (token, _) = PasswordResetToken.Issue(1, Now, Lifetime);

        Assert.Equal(PasswordResetPurpose.Ticket, token.Purpose);
    }

    [Fact]
    public void RegisterFailure_RevokesCodeAfterMaxAttempts()
    {
        var (token, _) = PasswordResetToken.IssueCode(1, Now, TimeSpan.FromSeconds(60));

        for (var i = 1; i < 5; i++)
        {
            token.RegisterFailure(Now.AddSeconds(i), maxAttempts: 5);
            Assert.True(token.IsUsable(Now.AddSeconds(i)));
        }

        token.RegisterFailure(Now.AddSeconds(5), maxAttempts: 5);

        Assert.Equal(5, token.FailedAttempts);
        Assert.False(token.IsUsable(Now.AddSeconds(6)));
    }

    [Fact]
    public void MarkUsed_Ticket_ExpiredUsesTicketMessage()
    {
        var (ticket, _) = PasswordResetToken.Issue(1, Now, Lifetime);
        var (code, _) = PasswordResetToken.IssueCode(1, Now, TimeSpan.FromSeconds(60));

        var ticketEx = Assert.Throws<UserFriendlyException>(() => ticket.MarkUsed(Now.AddHours(1)));
        var codeEx = Assert.Throws<UserFriendlyException>(() => code.MarkUsed(Now.AddHours(1)));

        Assert.Equal(PasswordResetToken.InvalidTicketMessage, ticketEx.Message);
        Assert.Equal(PasswordResetToken.InvalidMessage, codeEx.Message);
    }
}
