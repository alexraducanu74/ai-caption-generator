using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaptionGenerator.Web.Models;
using CaptionGenerator.Web.Models.Auth;

namespace CaptionGenerator.Web.Services;

public class AuthApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateStore _authStateStore;

    public AuthApiClient(HttpClient httpClient, AuthStateStore authStateStore)
    {
        _httpClient = httpClient;
        _authStateStore = authStateStore;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/register", request);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

            if (!response.IsSuccessStatusCode)
            {
                return (false, apiResponse?.Message ?? "Registration failed.");
            }

            return (true, apiResponse?.Message ?? "Registered successfully.");
        }
        catch (HttpRequestException)
        {
            return (false, "Unable to reach the authentication service right now. Please try again in a moment.");
        }
        catch (NotSupportedException)
        {
            return (false, "The authentication service returned an unexpected response.");
        }
        catch
        {
            return (false, "The authentication service returned an invalid response.");
        }
    }

    public async Task<(bool Success, string Message)> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

            if (!response.IsSuccessStatusCode || apiResponse?.Data is null)
            {
                return (false, apiResponse?.Message ?? "Login failed.");
            }

            var session = new AuthSession(apiResponse.Data.Token, apiResponse.Data.Email, apiResponse.Data.ExpiresAtUtc);
            await _authStateStore.SetSessionAsync(session);

            return (true, apiResponse.Message ?? "Login successful.");
        }
        catch (HttpRequestException)
        {
            return (false, "Unable to reach the authentication service right now. Please try again in a moment.");
        }
        catch (NotSupportedException)
        {
            return (false, "The authentication service returned an unexpected response.");
        }
        catch
        {
            return (false, "The authentication service returned an invalid response.");
        }
    }

    public async Task LogoutAsync()
    {
        await _authStateStore.ClearAsync();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task ApplyAuthHeaderAsync()
    {
        await _authStateStore.InitializeAsync();
        var token = _authStateStore.GetToken();

        _httpClient.DefaultRequestHeaders.Authorization = token is null
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }
}
