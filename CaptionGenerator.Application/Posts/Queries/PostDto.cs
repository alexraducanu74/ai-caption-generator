namespace CaptionGenerator.Application.Posts.Queries;

public record PostDto(Guid id, string caption, string platform, string status, string imageUrl, DateTime createdAtUtc);
