﻿using CaptionGenerator.Application.Behaviors;
using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Posts.Validators;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;
using MediatR;
using Moq;

namespace CaptionGenerator.Tests.Unit;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithValidRequest_ShouldCallNext()
    {
        var mockNext = new Mock<RequestHandlerDelegate<Guid>>();
        mockNext.Setup(x => x.Invoke()).ReturnsAsync(Guid.NewGuid());

        var command = new RegisterPostCommand("Valid Caption", SocialPlatform.Instagram, "https://cdn.example.com/test.jpg");

        var behavior = new ValidationBehavior<RegisterPostCommand, Guid>(
            new[] { new RegisterPostValidator() });

        await behavior.Handle(command, mockNext.Object, CancellationToken.None);

        mockNext.Verify(x => x.Invoke(), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldThrowValidationException()
    {
        var mockNext = new Mock<RequestHandlerDelegate<Guid>>();
        var command = new RegisterPostCommand("", SocialPlatform.Instagram, "https://cdn.example.com/test.jpg");

        var behavior = new ValidationBehavior<RegisterPostCommand, Guid>(
            new[] { new RegisterPostValidator() });

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => behavior.Handle(command, mockNext.Object, CancellationToken.None));

        mockNext.Verify(x => x.Invoke(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMultipleValidationErrors_ShouldThrowWithAllErrors()
    {
        var mockNext = new Mock<RequestHandlerDelegate<Guid>>();
        var invalidCaption = new string('a', 281);
        var command = new RegisterPostCommand(invalidCaption, (SocialPlatform)999, "invalid-url");

        var behavior = new ValidationBehavior<RegisterPostCommand, Guid>(
            new[] { new RegisterPostValidator() });

        var exception = await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => behavior.Handle(command, mockNext.Object, CancellationToken.None));

        exception.Errors.Should().HaveCount(3);
    }
}
