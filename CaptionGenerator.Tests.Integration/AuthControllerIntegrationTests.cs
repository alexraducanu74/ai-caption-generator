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

    [Fact]
    public async Task Register_WithDomainMissingTopLevelSuffix_ShouldReturn400BadRequest()
    {
        var email = $"invalid_{Guid.NewGuid():N}@b";
        const string password = "secret1";

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithValidEmail_ShouldReturn201Created()
    {
        var email = $"valid_{Guid.NewGuid():N}@example.com";
        const string password = "secret1";

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

