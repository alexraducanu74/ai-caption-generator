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

        var post = Post.Create(caption, platform);

        post.Should().NotBeNull();
        post.Caption.Should().Be(caption);
        post.Platform.Should().Be(platform);
        post.Status.Should().Be(PostStatus.Scheduled);
    }

    [Fact]
    public void Create_WithNullCaption_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Post.Create(null!, SocialPlatform.Facebook));
    }

    [Fact]
    public void Create_WithEmptyCaption_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Post.Create("", SocialPlatform.Facebook));
    }

    [Fact]
    public void MarkAsPublished_ShouldChangeStatusToPublished()
    {
        var post = Post.Create("Test", SocialPlatform.Instagram);

        post.MarkAsPublished();

        post.Status.Should().Be(PostStatus.Published);
    }
}

