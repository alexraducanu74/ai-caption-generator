using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaptionGenerator.API.Models;
using CaptionGenerator.API.Models.Requests;
using FluentAssertions;

namespace CaptionGenerator.Tests.Integration;

public class PostsControllerIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;

    public PostsControllerIntegrationTests(IntegrationTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePost_WithValidRequest_ShouldReturn201Created()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest("Test Caption", 1);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePost_WithEmptyCaption_ShouldReturn400BadRequest()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest("", 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePost_WithCaptionExceeding100Chars_ShouldReturn400()
    {
        await AuthenticateAsync();

        var longCaption = new string('a', 101);
        var request = new RegisterPostRequest(longCaption, 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllPosts_ShouldReturn200Ok()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/v1/posts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPostById_WithInvalidId_ShouldReturn404NotFound()
    {
        await AuthenticateAsync();

        var invalidId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/v1/posts/{invalidId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePost_WithInvalidId_ShouldReturn404NotFound()
    {
        await AuthenticateAsync();

        var invalidId = Guid.NewGuid();
        var response = await _client.DeleteAsync($"/api/v1/posts/{invalidId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePost_WithMaxLengthCaption_ShouldSucceed()
    {
        await AuthenticateAsync();

        var maxCaption = new string('a', 100);
        var request = new RegisterPostRequest(maxCaption, 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetAllPosts_WithoutToken_ShouldReturn401Unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/posts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task AuthenticateAsync()
    {
        var email = $"integration_{Guid.NewGuid():N}@example.com";
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
