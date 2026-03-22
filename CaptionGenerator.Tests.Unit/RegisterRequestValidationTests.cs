using System.ComponentModel.DataAnnotations;
using CaptionGenerator.API.Models.Requests;
using FluentAssertions;

namespace CaptionGenerator.Tests.Unit;

public class RegisterRequestValidationTests
{
    [Theory]
    [InlineData("invalid-no-at")]
    [InlineData("invalid@b")]
    [InlineData("a@a.com>>>>>")]
    [InlineData(".startdot@example.com")]
    [InlineData("enddot.@example.com")]
    [InlineData("double..dot@example.com")]
    [InlineData("user@-example.com")]
    [InlineData("user@example..com")]
    [InlineData("user@example.c")]
    public void RegisterRequest_WithMalformedEmail_ShouldFailValidation(string email)
    {
        var request = new RegisterRequest(email, "secret1");

        var result = Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.MemberNames.Contains(nameof(RegisterRequest.Email)));
    }

    [Theory]
    [InlineData("valid@example.com")]
    [InlineData("user.name+tag@example.co.uk")]
    [InlineData("u_ser-123@example-domain.com")]
    public void RegisterRequest_WithValidEmail_ShouldPassValidation(string email)
    {
        var request = new RegisterRequest(email, "secret1");

        var result = Validate(request);

        result.IsValid.Should().BeTrue();
    }

    private static (bool IsValid, List<ValidationResult> Errors) Validate(RegisterRequest request)
    {
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true);
        return (isValid, validationResults);
    }
}

