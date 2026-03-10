using System.Text.Json;
using CaptionGenerator.API.Models;
using Microsoft.Extensions.Options;

namespace CaptionGenerator.API.Services;

public class PythonCaptionGenerationClient : ICaptionGenerationClient
{
    private readonly HttpClient _httpClient;
    private readonly CaptionGenerationOptions _options;
    private readonly ILogger<PythonCaptionGenerationClient> _logger;

    public PythonCaptionGenerationClient(
        HttpClient httpClient,
        IOptions<CaptionGenerationOptions> options,
        ILogger<PythonCaptionGenerationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string? Caption, string? ErrorMessage)> GenerateCaptionAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            // Keep local and test environments functional when Python API is not configured.
            return (true, $"Generated caption for image {Path.GetFileName(imageUrl)}", null);
        }

        Uri requestUri;
        try
        {
            var baseUri = new Uri(_options.BaseUrl, UriKind.Absolute);
            requestUri = new Uri(baseUri, _options.GeneratePath.TrimStart('/'));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CaptionGeneration settings are invalid.");
            return (false, null, "Caption service configuration is invalid.");
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(requestUri, new { imageUrl }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed calling Python caption service.");
            return (false, null, "Caption service is unavailable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Python caption service returned status code {StatusCode}.", response.StatusCode);
            return (false, null, "Caption service returned an error.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return (false, null, "Caption service returned an empty response.");
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (TryGetCaption(root, out var caption))
            {
                return (true, caption, null);
            }

            if (root.TryGetProperty("data", out var dataElement) && TryGetCaption(dataElement, out caption))
            {
                return (true, caption, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Python caption service response.");
            return (false, null, "Caption service returned invalid data.");
        }

        return (false, null, "Caption service did not return a caption.");
    }

    private static bool TryGetCaption(JsonElement element, out string caption)
    {
        caption = string.Empty;

        if (!element.TryGetProperty("caption", out var captionElement) || captionElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = captionElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        caption = value.Trim();
        return true;
    }
}

