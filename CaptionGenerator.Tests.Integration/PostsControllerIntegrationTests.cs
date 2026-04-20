using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaptionGenerator.API.Models;
using CaptionGenerator.API.Models.Requests;
using CaptionGenerator.Domain.Enums;
using CaptionGenerator.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CaptionGenerator.Tests.Integration;

public class PostsControllerIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public PostsControllerIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePost_WithValidRequest_ShouldReturn201Created()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest(1, "https://cdn.example.com/test.jpg", DateTime.UtcNow.AddMinutes(15));
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePost_WithEmptyImageUrl_ShouldReturn400BadRequest()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest(0, "", DateTime.UtcNow.AddMinutes(15));
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePost_WithInvalidPlatform_ShouldReturn400()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest(999, "https://cdn.example.com/test.jpg", DateTime.UtcNow.AddMinutes(15));
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
    public async Task CreatePost_WithValidImageUrl_ShouldSucceed()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest(0, "https://cdn.example.com/another.jpg", DateTime.UtcNow.AddMinutes(15));
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdatePost_WithValidRequest_ShouldReturn200AndPersistChanges()
    {
        await AuthenticateAsync();

        var postId = await CreatePostAsync();
        var updateRequest = new UpdatePostRequest("Updated caption", "https://cdn.example.com/updated.jpg");

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/posts/{postId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/v1/posts/{postId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        payload.Should().NotBeNull();
        payload!.Data.Should().NotBeNull();
        payload.Data!.Caption.Should().Be("Updated caption");
        payload.Data.ImageUrl.Should().Be("https://cdn.example.com/updated.jpg");
    }

    [Fact]
    public async Task UpdatePost_WithInvalidId_ShouldReturn404NotFound()
    {
        await AuthenticateAsync();

        var invalidId = Guid.NewGuid();
        var updateRequest = new UpdatePostRequest("Updated caption", "https://cdn.example.com/updated.jpg");

        var response = await _client.PutAsJsonAsync($"/api/v1/posts/{invalidId}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePost_WithExistingId_ShouldReturn200AndPostShouldNotBeFound()
    {
        await AuthenticateAsync();

        var postId = await CreatePostAsync();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/posts/{postId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/v1/posts/{postId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllPosts_WithoutToken_ShouldReturn401Unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/posts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePost_WithPastScheduledDate_ShouldReturn400BadRequest()
    {
        await AuthenticateAsync();

        var request = new RegisterPostRequest(0, "https://cdn.example.com/past.jpg", DateTime.UtcNow.AddMinutes(-5));
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmPostPublication_WhenPostIsPending_ShouldReturn200AndPublish()
    {
        await AuthenticateAsync();

        var postId = await CreatePostAsync();
        await SetPostStatusAsync(postId, PostStatus.PendingConfirmation);

        var response = await _client.PostAsync($"/api/v1/posts/{postId}/confirm", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/v1/posts/{postId}");
        var payload = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        payload.Should().NotBeNull();
        payload!.Data!.Status.Should().Be("Published");
    }

    [Fact]
    public async Task CancelPostPublication_WhenPostIsPending_ShouldReturn200AndCancel()
    {
        await AuthenticateAsync();

        var postId = await CreatePostAsync();
        await SetPostStatusAsync(postId, PostStatus.PendingConfirmation);

        var response = await _client.PostAsync($"/api/v1/posts/{postId}/cancel", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/v1/posts/{postId}");
        var payload = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>();
        payload.Should().NotBeNull();
        payload!.Data!.Status.Should().Be("Canceled");
    }

    private async Task<Guid> CreatePostAsync()
    {
        var request = new RegisterPostRequest(1, $"https://cdn.example.com/{Guid.NewGuid():N}.jpg", DateTime.UtcNow.AddMinutes(15));
        var response = await _client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        payload.Should().NotBeNull();
        payload!.Data.Should().NotBe(Guid.Empty);

        return payload.Data;
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

    private async Task SetPostStatusAsync(Guid postId, PostStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var post = await dbContext.Posts.FindAsync(postId);
        post.Should().NotBeNull();

        if (status == PostStatus.PendingConfirmation)
        {
            post!.MarkAsPendingConfirmation();
        }
        else if (status == PostStatus.Published)
        {
            post!.MarkAsPublished();
        }
        else if (status == PostStatus.Canceled)
        {
            post!.MarkAsCanceled();
        }

        await dbContext.SaveChangesAsync();
    }
}
