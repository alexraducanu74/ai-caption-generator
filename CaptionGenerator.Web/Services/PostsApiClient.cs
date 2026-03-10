using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaptionGenerator.Web.Models;
using CaptionGenerator.Web.Models.Posts;
using Microsoft.AspNetCore.Components.Forms;

namespace CaptionGenerator.Web.Services;

public class PostsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateStore _authStateStore;

    public PostsApiClient(HttpClient httpClient, AuthStateStore authStateStore)
    {
        _httpClient = httpClient;
        _authStateStore = authStateStore;
    }

    public async Task<(bool Success, string Message, string? ImageUrl)> UploadImageAsync(IBrowserFile file)
    {
        await ApplyAuthHeaderAsync();

        using var multipart = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(5 * 1024 * 1024);
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        multipart.Add(streamContent, "file", file.Name);

        var response = await _httpClient.PostAsync("api/v1/uploads/images", multipart);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<UploadImageResponse>>();

        if (!response.IsSuccessStatusCode || payload?.Data is null)
        {
            return (false, payload?.Message ?? "Image upload failed.", null);
        }

        return (true, payload.Message ?? "Image uploaded successfully.", payload.Data.ImageUrl);
    }

    public async Task<(bool Success, string Message)> CreatePostAsync(CreatePostRequest request)
    {
        await ApplyAuthHeaderAsync();

        var response = await _httpClient.PostAsJsonAsync("api/v1/posts", request);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();

        if (!response.IsSuccessStatusCode)
        {
            return (false, payload?.Message ?? "Create post failed.");
        }

        return (true, payload?.Message ?? "Post created successfully.");
    }

    public async Task<List<PostResponse>> GetPostsAsync()
    {
        await ApplyAuthHeaderAsync();

        var response = await _httpClient.GetAsync("api/v1/posts");
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<List<PostResponse>>>();
        return payload?.Data ?? [];
    }

    private async Task ApplyAuthHeaderAsync()
    {
        await _authStateStore.InitializeAsync();
        var token = _authStateStore.GetToken();

        _httpClient.DefaultRequestHeaders.Authorization = token is null
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }
}

