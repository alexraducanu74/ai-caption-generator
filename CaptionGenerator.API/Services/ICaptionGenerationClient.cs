﻿namespace CaptionGenerator.API.Services;

public interface ICaptionGenerationClient
{
    /// <summary>
    /// Generate a caption from a public image URL (used by tests / when bytes are unavailable).
    /// </summary>
    Task<(bool Success, string? Caption, string? ErrorMessage)> GenerateCaptionAsync(string imageUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Generate a caption by uploading the raw image bytes directly — avoids cross-container HTTP fetches.
    /// </summary>
    Task<(bool Success, string? Caption, string? ErrorMessage)> GenerateCaptionFromBytesAsync(byte[] imageBytes, string fileName, CancellationToken cancellationToken);
}

