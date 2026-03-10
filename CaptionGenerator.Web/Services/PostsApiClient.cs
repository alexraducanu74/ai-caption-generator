using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        try
        {
            await ApplyAuthHeaderAsync();

            using var multipart = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream(5 * 1024 * 1024);
            using var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            multipart.Add(streamContent, "file", file.Name);

            var response = await _httpClient.PostAsync("api/v1/uploads/images", multipart);
            var payload = await SafeReadJsonAsync<ApiResponse<UploadImageResponse>>(response);

            if (!response.IsSuccessStatusCode || payload?.Data is null)
            {
                return (false, payload?.Message ?? $"Image upload failed (HTTP {(int)response.StatusCode}).", null);
            }

            return (true, payload.Message ?? "Image uploaded successfully.", payload.Data.ImageUrl);
        }
        catch (Exception ex)
        {
            return (false, $"Upload error: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message)> CreatePostAsync(CreatePostRequest request)
    {
        try
        {
            await ApplyAuthHeaderAsync();

            var response = await _httpClient.PostAsJsonAsync("api/v1/posts", request);
            var payload = await SafeReadJsonAsync<ApiResponse<Guid>>(response);

            if (!response.IsSuccessStatusCode)
            {
                return (false, payload?.Message ?? $"Create post failed (HTTP {(int)response.StatusCode}).");
            }

            return (true, payload?.Message ?? "Post created successfully.");
        }
        catch (Exception ex)
        {
            return (false, $"Create post error: {ex.Message}");
        }
    }

    public async Task<List<PostResponse>> GetPostsAsync()
    {
        try
        {
            await ApplyAuthHeaderAsync();

            var response = await _httpClient.GetAsync("api/v1/posts");
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var payload = await SafeReadJsonAsync<ApiResponse<List<PostResponse>>>(response);
            return payload?.Data ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task<T?> SafeReadJsonAsync<T>(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body)) return default;
            return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return default;
        }
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
