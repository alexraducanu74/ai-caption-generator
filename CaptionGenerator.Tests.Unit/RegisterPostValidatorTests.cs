using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Posts.Validators;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;

namespace CaptionGenerator.Tests.Unit;

public class RegisterPostValidatorTests
{
    private readonly RegisterPostValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCommand_ShouldPass()
    {
        var command = new RegisterPostCommand("Test Caption", SocialPlatform.Instagram, "https://cdn.example.com/test.jpg", DateTime.UtcNow.AddMinutes(15));
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyCaption_ShouldFail()
    {
        var command = new RegisterPostCommand("", SocialPlatform.Instagram, "https://cdn.example.com/test.jpg", DateTime.UtcNow.AddMinutes(15));
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "caption");
    }

    [Fact]
    public async Task Validate_WithCaptionExceeding280Chars_ShouldFail()
    {
        var longCaption = new string('a', 281);
        var command = new RegisterPostCommand(longCaption, SocialPlatform.Facebook, "https://cdn.example.com/test.jpg", DateTime.UtcNow.AddMinutes(15));
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "caption");
    }

    [Fact]
    public async Task Validate_WithInvalidPlatform_ShouldFail()
    {
        var command = new RegisterPostCommand("Valid Caption", (SocialPlatform)999, "https://cdn.example.com/test.jpg", DateTime.UtcNow.AddMinutes(15));
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "platform");
    }

    [Fact]
    public async Task Validate_WithInvalidImageUrl_ShouldFail()
    {
        var command = new RegisterPostCommand("Valid Caption", SocialPlatform.Instagram, "not-a-url", DateTime.UtcNow.AddMinutes(15));
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "imageUrl");
    }

    [Fact]
    public async Task Validate_WithPastScheduledDate_ShouldFail()
    {
        var command = new RegisterPostCommand("Valid Caption", SocialPlatform.Instagram, "https://cdn.example.com/test.jpg", DateTime.UtcNow.AddMinutes(-1));
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "scheduledForUtc");
    }
}
