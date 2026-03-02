using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Domain.Entities;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;
using Moq;

namespace CaptionGenerator.Tests.Unit;

public class RegisterPostCommandHandlerTests
{
    private readonly Mock<IPostsRepository> _repositoryMock = new();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreatePostAndReturnId()
    {
        var handler = new RegisterPostCommandHandler(_repositoryMock.Object);
        var command = new RegisterPostCommand("New Post", SocialPlatform.Instagram);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldHaveScheduledStatus()
    {
        Post? capturedPost = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()))
            .Callback<Post, CancellationToken>((post, _) => capturedPost = post)
            .Returns(Task.CompletedTask);

        var handler = new RegisterPostCommandHandler(_repositoryMock.Object);
        var command = new RegisterPostCommand("Test Caption", SocialPlatform.Facebook);

        await handler.Handle(command, CancellationToken.None);

        capturedPost.Should().NotBeNull();
        capturedPost!.Status.Should().Be(PostStatus.Scheduled);
        capturedPost.Caption.Should().Be("Test Caption");
        capturedPost.Platform.Should().Be(SocialPlatform.Facebook);
    }
}

