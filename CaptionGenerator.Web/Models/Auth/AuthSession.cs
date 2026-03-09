namespace CaptionGenerator.Web.Models.Auth;

public record AuthSession(string Token, string Email, DateTime ExpiresAtUtc);

