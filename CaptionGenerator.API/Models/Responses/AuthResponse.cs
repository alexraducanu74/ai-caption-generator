namespace CaptionGenerator.API.Models;

public record AuthResponse(string Token, string Email, DateTime ExpiresAtUtc);

