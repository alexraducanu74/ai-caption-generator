﻿using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Domain.Entities;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;
using Moq;

namespace CaptionGenerator.Tests.Unit;

public class DeletePostCommandHandlerTests
{
    private readonly Mock<IPostsRepository> _repositoryMock = new();

    [Fact]
    public async Task Handle_WithValidId_ShouldDeletePostAndReturnTrue()
    {
        var postId = Guid.NewGuid();
        var post = Post.Create("Test Post", SocialPlatform.Facebook, "https://cdn.example.com/a.jpg");

        _repositoryMock.Setup(r => r.FindAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var handler = new DeletePostCommandHandler(_repositoryMock.Object);
        var command = new DeletePostCommand(postId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeTrue();
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidId_ShouldReturnFalse()
    {
        var postId = Guid.NewGuid();

        _repositoryMock.Setup(r => r.FindAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var handler = new DeletePostCommandHandler(_repositoryMock.Object);
        var command = new DeletePostCommand(postId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeFalse();
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryFindAsyncWithCorrectId()
    {
        var postId = Guid.NewGuid();
        var post = Post.Create("Test Post", SocialPlatform.Instagram, "https://cdn.example.com/b.jpg");

        _repositoryMock.Setup(r => r.FindAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var handler = new DeletePostCommandHandler(_repositoryMock.Object);
        var command = new DeletePostCommand(postId);

        await handler.Handle(command, CancellationToken.None);

        _repositoryMock.Verify(r => r.FindAsync(postId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
