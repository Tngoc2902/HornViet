namespace Website.Api.Models.Account;

public class AuthenticateResultModel
{
    public string AccessToken { get; set; } = null!;
    public int ExpireInSeconds { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
