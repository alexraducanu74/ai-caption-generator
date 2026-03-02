using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Posts.Validators;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;
using FluentValidation;

namespace CaptionGenerator.Tests.Unit;

public class RegisterPostValidatorTests
{
    private readonly RegisterPostValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCommand_ShouldPass()
    {
        var command = new RegisterPostCommand("Test Caption", SocialPlatform.Instagram);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyCaption_ShouldFail()
    {
        var command = new RegisterPostCommand("", SocialPlatform.Instagram);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "caption");
    }

    [Fact]
    public async Task Validate_WithCaptionExceeding100Chars_ShouldFail()
    {
        var longCaption = new string('a', 101);
        var command = new RegisterPostCommand(longCaption, SocialPlatform.Facebook);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "caption");
    }

    [Fact]
    public async Task Validate_WithInvalidPlatform_ShouldFail()
    {
        var command = new RegisterPostCommand("Valid Caption", (SocialPlatform)999);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "platform");
    }
}

