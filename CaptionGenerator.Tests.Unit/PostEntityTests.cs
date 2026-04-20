using CaptionGenerator.Domain.Entities;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;

namespace CaptionGenerator.Tests.Unit;

public class PostEntityTests
{
    [Fact]
    public void Create_WithValidCaptionAndPlatform_ShouldCreatePost()
    {
        var caption = "Test Caption";
        var platform = SocialPlatform.Instagram;
        var imageUrl = "https://cdn.example.com/test.jpg";
        var scheduledForUtc = DateTime.UtcNow.AddMinutes(30);

        var post = Post.Create(caption, platform, imageUrl, scheduledForUtc);

        post.Should().NotBeNull();
        post.Caption.Should().Be(caption);
        post.Platform.Should().Be(platform);
        post.ImageUrl.Should().Be(imageUrl);
        post.CreatedAtUtc.Should().BeOnOrBefore(DateTime.UtcNow);
        post.ScheduledForUtc.Should().BeCloseTo(scheduledForUtc, TimeSpan.FromSeconds(1));
        post.Status.Should().Be(PostStatus.Scheduled);
    }

    [Fact]
    public void Create_WithNullCaption_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Post.Create(null!, SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30)));
    }

    [Fact]
    public void Create_WithEmptyCaption_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Post.Create("", SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30)));
    }

    [Fact]
    public void Create_WithEmptyImageUrl_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Post.Create("Caption", SocialPlatform.Facebook, "", DateTime.UtcNow.AddMinutes(30)));
    }

    [Fact]
    public void Create_WithDefaultScheduledDate_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Post.Create("Caption", SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", default));
    }

    [Fact]
    public void MarkAsPublished_ShouldChangeStatusToPublished()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));

        post.MarkAsPublished();

        post.Status.Should().Be(PostStatus.Published);
    }

    [Fact]
    public void MarkAsPendingConfirmation_ShouldChangeStatusToPendingConfirmation()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));

        post.MarkAsPendingConfirmation();

        post.Status.Should().Be(PostStatus.PendingConfirmation);
    }

    [Fact]
    public void MarkAsCanceled_ShouldChangeStatusToCanceled()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));

        post.MarkAsCanceled();

        post.Status.Should().Be(PostStatus.Canceled);
    }

    [Fact]
    public void UpdateDetails_WithValidValues_ShouldUpdateCaptionAndImageUrl()
    {
        var post = Post.Create("Old", SocialPlatform.Instagram, "https://cdn.example.com/old.jpg", DateTime.UtcNow.AddMinutes(30));

        post.UpdateDetails("New", "https://cdn.example.com/new.jpg");

        post.Caption.Should().Be("New");
        post.ImageUrl.Should().Be("https://cdn.example.com/new.jpg");
    }

    [Fact]
    public void UpdateDetails_WithEmptyCaption_ShouldThrowArgumentNullException()
    {
        var post = Post.Create("Old", SocialPlatform.Instagram, "https://cdn.example.com/old.jpg", DateTime.UtcNow.AddMinutes(30));

        Assert.Throws<ArgumentNullException>(() => post.UpdateDetails("", "https://cdn.example.com/new.jpg"));
    }

    [Fact]
    public void UpdateDetails_WithEmptyImageUrl_ShouldThrowArgumentNullException()
    {
        var post = Post.Create("Old", SocialPlatform.Instagram, "https://cdn.example.com/old.jpg", DateTime.UtcNow.AddMinutes(30));

        Assert.Throws<ArgumentNullException>(() => post.UpdateDetails("New", ""));
    }
}
