namespace CaptionGenerator.API.Models;

public record PostResponse(Guid Id, string Caption, string Platform, string Status, string ImageUrl, DateTime CreatedAtUtc, DateTime ScheduledForUtc);
