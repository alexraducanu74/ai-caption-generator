using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Domain.Entities;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;
using Moq;

namespace CaptionGenerator.Tests.Unit;

public class UpdatePostCommandHandlerTests
{
    private readonly Mock<IPostsRepository> _repositoryMock = new();

    [Fact]
    public async Task Handle_WithExistingPost_ShouldUpdateAndReturnTrue()
    {
        var postId = Guid.NewGuid();
        var post = Post.Create("Old caption", SocialPlatform.Instagram, "https://cdn.example.com/old.jpg");

        _repositoryMock.Setup(r => r.FindAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var handler = new UpdatePostCommandHandler(_repositoryMock.Object);
        var command = new UpdatePostCommand(postId, "New caption", "https://cdn.example.com/new.jpg");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeTrue();
        post.Caption.Should().Be("New caption");
        post.ImageUrl.Should().Be("https://cdn.example.com/new.jpg");
        _repositoryMock.Verify(r => r.UpdateAsync(post, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMissingPost_ShouldReturnFalse()
    {
        var postId = Guid.NewGuid();

        _repositoryMock.Setup(r => r.FindAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var handler = new UpdatePostCommandHandler(_repositoryMock.Object);
        var command = new UpdatePostCommand(postId, "Any", "https://cdn.example.com/x.jpg");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeFalse();
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

