using CaptionGenerator.Domain.Entities;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;

namespace CaptionGenerator.Tests.Unit;

public class RepositoryTests
{
    [Fact]
    public void Post_Create_WithValidData_ShouldInitializeCorrectly()
    {
        var caption = "Test Caption";
        var platform = SocialPlatform.Facebook;
        var imageUrl = "https://cdn.example.com/test.jpg";
        var scheduledForUtc = DateTime.UtcNow.AddMinutes(30);

        var post = Post.Create(caption, platform, imageUrl, scheduledForUtc);

        post.Should().NotBeNull();
        post.Id.Should().NotBeEmpty();
        post.Caption.Should().Be(caption);
        post.Platform.Should().Be(platform);
        post.ImageUrl.Should().Be(imageUrl);
        post.CreatedAtUtc.Should().BeOnOrBefore(DateTime.UtcNow);
        post.ScheduledForUtc.Should().BeCloseTo(scheduledForUtc, TimeSpan.FromSeconds(1));
        post.Status.Should().Be(PostStatus.Scheduled);
    }

    [Fact]
    public void Post_MarkAsPublished_ShouldChangeStatus()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));

        post.MarkAsPublished();

        post.Status.Should().Be(PostStatus.Published);
    }

    [Fact]
    public void Post_Create_WithNullCaption_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Post.Create(null!, SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30)));
    }

    [Fact]
    public void Post_Create_WithEmptyCaption_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Post.Create(string.Empty, SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30)));
    }

    [Fact]
    public void Post_ShouldHaveUniqueIds()
    {
        var post1 = Post.Create("Post 1", SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));
        var post2 = Post.Create("Post 2", SocialPlatform.Instagram, "https://cdn.example.com/b.jpg", DateTime.UtcNow.AddMinutes(35));

        post1.Id.Should().NotBe(post2.Id);
    }

    [Fact]
    public void Post_MarkAsPendingConfirmation_ShouldChangeStatus()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));

        post.MarkAsPendingConfirmation();

        post.Status.Should().Be(PostStatus.PendingConfirmation);
    }

    [Fact]
    public void Post_MarkAsCanceled_ShouldChangeStatus()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));

        post.MarkAsCanceled();

        post.Status.Should().Be(PostStatus.Canceled);
    }
}
