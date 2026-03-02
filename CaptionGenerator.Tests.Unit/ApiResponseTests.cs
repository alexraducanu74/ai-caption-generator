using CaptionGenerator.API.Models;
using FluentAssertions;

namespace CaptionGenerator.Tests.Unit;

public class ApiResponseTests
{
    [Fact]
    public void SuccessResponse_WithData_ShouldCreateCorrectResponse()
    {
        var data = Guid.NewGuid();
        var message = "Success";

        var response = ApiResponse<Guid>.SuccessResponse(data, message);

        response.Success.Should().BeTrue();
        response.Message.Should().Be(message);
        response.Data.Should().Be(data);
        response.Errors.Should().BeNull();
    }

    [Fact]
    public void ErrorResponse_WithMessage_ShouldCreateCorrectResponse()
    {
        var message = "Error occurred";

        var response = ApiResponse<object>.ErrorResponse(message);

        response.Success.Should().BeFalse();
        response.Message.Should().Be(message);
        response.Data.Should().BeNull();
    }

    [Fact]
    public void ErrorResponse_WithErrors_ShouldIncludeErrors()
    {
        var message = "Validation failed";
        var errors = new Dictionary<string, string[]>
        {
            { "Field1", new[] { "Field 1 is required" } },
            { "Field2", new[] { "Field 2 is invalid" } }
        };

        var response = ApiResponse<object>.ErrorResponse(message, errors);

        response.Success.Should().BeFalse();
        response.Message.Should().Be(message);
        response.Errors.Should().HaveCount(2);
        response.Errors.Should().ContainKey("Field1");
        response.Errors.Should().ContainKey("Field2");
    }

    [Fact]
    public void SuccessResponse_WithList_ShouldHandleCollections()
    {
        var data = new List<string> { "Item1", "Item2", "Item3" };
        var message = "List retrieved";

        var response = ApiResponse<List<string>>.SuccessResponse(data, message);

        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(3);
    }
}



