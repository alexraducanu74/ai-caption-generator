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

        var post = Post.Create(caption, platform, imageUrl);

        post.Should().NotBeNull();
        post.Id.Should().NotBeEmpty();
        post.Caption.Should().Be(caption);
        post.Platform.Should().Be(platform);
        post.ImageUrl.Should().Be(imageUrl);
        post.CreatedAtUtc.Should().BeOnOrBefore(DateTime.UtcNow);
        post.Status.Should().Be(PostStatus.Scheduled);
    }

    [Fact]
    public void Post_MarkAsPublished_ShouldChangeStatus()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram, "https://cdn.example.com/a.jpg");

        post.MarkAsPublished();

        post.Status.Should().Be(PostStatus.Published);
    }

    [Fact]
    public void Post_Create_WithNullCaption_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Post.Create(null!, SocialPlatform.Facebook, "https://cdn.example.com/a.jpg"));
    }

    [Fact]
    public void Post_Create_WithEmptyCaption_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Post.Create(string.Empty, SocialPlatform.Facebook, "https://cdn.example.com/a.jpg"));
    }

    [Fact]
    public void Post_ShouldHaveUniqueIds()
    {
        var post1 = Post.Create("Post 1", SocialPlatform.Facebook, "https://cdn.example.com/a.jpg");
        var post2 = Post.Create("Post 2", SocialPlatform.Instagram, "https://cdn.example.com/b.jpg");

        post1.Id.Should().NotBe(post2.Id);
    }
}
