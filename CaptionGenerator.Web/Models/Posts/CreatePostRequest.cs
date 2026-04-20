namespace CaptionGenerator.Web.Models.Posts;

public record CreatePostRequest(int Platform, string ImageUrl, DateTime ScheduledForUtc);
