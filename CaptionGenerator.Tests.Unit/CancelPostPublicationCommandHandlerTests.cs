using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Domain.Entities;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;
using Moq;

namespace CaptionGenerator.Tests.Unit;

public class CancelPostPublicationCommandHandlerTests
{
    private readonly Mock<IPostsRepository> _repositoryMock = new();

    [Fact]
    public async Task Handle_WithPendingPost_ShouldCancelAndReturnTrue()
    {
        var postId = Guid.NewGuid();
        var post = Post.Create("Caption", SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));
        post.MarkAsPendingConfirmation();

        _repositoryMock.Setup(r => r.FindAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var handler = new CancelPostPublicationCommandHandler(_repositoryMock.Object);

        var result = await handler.Handle(new CancelPostPublicationCommand(postId), CancellationToken.None);

        result.Should().BeTrue();
        post.Status.Should().Be(PostStatus.Canceled);
        _repositoryMock.Verify(r => r.UpdateAsync(post, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonPendingPost_ShouldReturnFalse()
    {
        var postId = Guid.NewGuid();
        var post = Post.Create("Caption", SocialPlatform.Facebook, "https://cdn.example.com/a.jpg", DateTime.UtcNow.AddMinutes(30));

        _repositoryMock.Setup(r => r.FindAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var handler = new CancelPostPublicationCommandHandler(_repositoryMock.Object);

        var result = await handler.Handle(new CancelPostPublicationCommand(postId), CancellationToken.None);

        result.Should().BeFalse();
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

