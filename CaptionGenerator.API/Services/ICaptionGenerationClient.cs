namespace CaptionGenerator.API.Services;

public interface ICaptionGenerationClient
{
    Task<(bool Success, string? Caption, string? ErrorMessage)> GenerateCaptionAsync(string imageUrl, CancellationToken cancellationToken);
}

