namespace CaptionGenerator.Web.Models.Auth;

public record AuthResponse(string Token, string Email, DateTime ExpiresAtUtc);

