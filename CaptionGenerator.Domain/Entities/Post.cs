using CaptionGenerator.Domain.Enums;

namespace CaptionGenerator.Domain.Entities;

public class Post
{
    private Post()
    {

    }

    public static Post Create(string caption, SocialPlatform platform, string imageUrl, DateTime scheduledForUtc)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentNullException(nameof(caption));
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentNullException(nameof(imageUrl));
        }

        if (scheduledForUtc == default)
        {
            throw new ArgumentNullException(nameof(scheduledForUtc));
        }

        return new Post
        {
            Id = Guid.NewGuid(),
            Caption = caption,
            Platform = platform,
            ImageUrl = imageUrl,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledForUtc = DateTime.SpecifyKind(scheduledForUtc, DateTimeKind.Utc),
            Status = PostStatus.Scheduled
        };
    }

    public Guid Id { get; private set; }
    public string Caption { get; private set; } = string.Empty;
    public SocialPlatform Platform { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ScheduledForUtc { get; private set; }
    public PostStatus Status { get; private set; }

    public void UpdateDetails(string caption, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentNullException(nameof(caption));
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentNullException(nameof(imageUrl));
        }

        Caption = caption;
        ImageUrl = imageUrl;
    }

    public void MarkAsPublished()
    {
        Status = PostStatus.Published;
    }

    public void MarkAsPendingConfirmation()
    {
        Status = PostStatus.PendingConfirmation;
    }

    public void MarkAsCanceled()
    {
        Status = PostStatus.Canceled;
    }
}
