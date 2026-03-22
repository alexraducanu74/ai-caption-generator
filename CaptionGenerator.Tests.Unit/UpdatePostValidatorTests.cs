using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Posts.Validators;
using FluentAssertions;

namespace CaptionGenerator.Tests.Unit;

public class UpdatePostValidatorTests
{
    private readonly UpdatePostValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCommand_ShouldPass()
    {
        var command = new UpdatePostCommand(Guid.NewGuid(), "Updated caption", "https://cdn.example.com/updated.jpg");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyCaption_ShouldFail()
    {
        var command = new UpdatePostCommand(Guid.NewGuid(), "", "https://cdn.example.com/updated.jpg");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "caption");
    }

    [Fact]
    public async Task Validate_WithInvalidImageUrl_ShouldFail()
    {
        var command = new UpdatePostCommand(Guid.NewGuid(), "Updated caption", "not-a-url");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "imageUrl");
    }
}

