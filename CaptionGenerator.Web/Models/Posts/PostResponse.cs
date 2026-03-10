namespace CaptionGenerator.Web.Models.Posts;

public record PostResponse(
    Guid Id,
    string Caption,
    string Platform,
    string Status,
    string ImageUrl,
    DateTime CreatedAtUtc);

