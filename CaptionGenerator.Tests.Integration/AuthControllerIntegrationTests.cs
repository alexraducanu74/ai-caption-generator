using System.Net;
using System.Net.Http.Json;
using CaptionGenerator.API.Models.Requests;
using FluentAssertions;

namespace CaptionGenerator.Tests.Integration;

public class AuthControllerIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;

    public AuthControllerIntegrationTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
    }

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
    public async Task Register_WithMalformedEmail_ShouldReturn400BadRequest(string email)
    {
        const string password = "secret1";

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("valid@example.com")]
    [InlineData("user.name+tag@example.co.uk")]
    [InlineData("u_ser-123@example-domain.com")]
    public async Task Register_WithValidEmail_ShouldReturn201Created(string emailPrefix)
    {
        var email = emailPrefix.Replace("valid", $"valid_{Guid.NewGuid():N}");
        const string password = "secret1";

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
