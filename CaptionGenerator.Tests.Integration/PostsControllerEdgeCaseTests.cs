using System.Net;
using System.Net.Http.Json;
using CaptionGenerator.API.Models;
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
        var request = new RegisterPostRequest("Valid Caption", 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePost_WithExceededLimit_ShouldFail()
    {
        var tooLongCaption = new string('a', 101);
        var request = new RegisterPostRequest(tooLongCaption, 0);
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllPosts_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/api/v1/posts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatePost_WithBothPlatforms_ShouldSucceed()
    {
        var requestFb = new RegisterPostRequest("Facebook Post", 0);
        var requestIg = new RegisterPostRequest("Instagram Post", 1);

        var responseFb = await _client.PostAsJsonAsync("/api/v1/posts", requestFb);
        var responseIg = await _client.PostAsJsonAsync("/api/v1/posts", requestIg);

        responseFb.StatusCode.Should().Be(HttpStatusCode.Created);
        responseIg.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
