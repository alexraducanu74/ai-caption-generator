using CaptionGenerator.Domain.Enums;

namespace CaptionGenerator.Domain.Entities;

public class Post
{
    private Post()
    {

    }

    public static Post Create(string caption, SocialPlatform platform, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new ArgumentNullException(nameof(caption));
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentNullException(nameof(imageUrl));
        }

        return new Post
        {
            Id = Guid.NewGuid(),
            Caption = caption,
            Platform = platform,
            ImageUrl = imageUrl,
            CreatedAtUtc = DateTime.UtcNow,
            Status = PostStatus.Scheduled
        };
    }

    public Guid Id { get; private set; }
    public string Caption { get; private set; } = string.Empty;
    public SocialPlatform Platform { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public PostStatus Status { get; private set; }

    public void MarkAsPublished()
    {
        Status = PostStatus.Published;
    }
}
