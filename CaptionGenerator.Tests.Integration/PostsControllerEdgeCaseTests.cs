using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaptionGenerator.API.Models;
using CaptionGenerator.API.Models.Requests;
using FluentAssertions;

namespace CaptionGenerator.Tests.Integration;

public class PostsControllerEdgeCaseTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;

    public PostsControllerEdgeCaseTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePost_WithValidRequest_ShouldCreate()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest(0, "https://cdn.example.com/valid.jpg");
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePost_WithEmptyImageUrl_ShouldFail()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest(0, "");
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllPosts_ShouldReturnOk()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/v1/posts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatePost_WithBothPlatforms_ShouldSucceed()
    {
        await AuthenticateAsync();

        var requestFb = new RegisterPostRequest(0, "https://cdn.example.com/fb.jpg");
        var requestIg = new RegisterPostRequest(1, "https://cdn.example.com/ig.jpg");

        var responseFb = await _client.PostAsJsonAsync("/api/v1/posts", requestFb);
        var responseIg = await _client.PostAsJsonAsync("/api/v1/posts", requestIg);

        responseFb.StatusCode.Should().Be(HttpStatusCode.Created);
        responseIg.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task AuthenticateAsync()
    {
        var email = $"edge_{Guid.NewGuid():N}@example.com";
        const string password = "secret1";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password));

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        payload.Should().NotBeNull();
        payload!.Data.Should().NotBeNull();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.Data!.Token);
    }
}
