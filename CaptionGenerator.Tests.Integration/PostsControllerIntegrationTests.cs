using System.Net;
using System.Net.Http.Json;
using CaptionGenerator.API.Models;
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
        var request = new RegisterPostRequest("Test Caption", 1);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePost_WithEmptyCaption_ShouldReturn400BadRequest()
    {
        var request = new RegisterPostRequest("", 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePost_WithCaptionExceeding100Chars_ShouldReturn400()
    {
        var longCaption = new string('a', 101);
        var request = new RegisterPostRequest(longCaption, 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllPosts_ShouldReturn200Ok()
    {
        var response = await _client.GetAsync("/api/v1/posts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPostById_WithInvalidId_ShouldReturn404NotFound()
    {
        var invalidId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/v1/posts/{invalidId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePost_WithInvalidId_ShouldReturn404NotFound()
    {
        var invalidId = Guid.NewGuid();
        var response = await _client.DeleteAsync($"/api/v1/posts/{invalidId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePost_WithMaxLengthCaption_ShouldSucceed()
    {
        var maxCaption = new string('a', 100);
        var request = new RegisterPostRequest(maxCaption, 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
