namespace CaptionGenerator.API.Models;

public class CaptionGenerationOptions
{
    public const string SectionName = "CaptionGeneration";

    public string? BaseUrl { get; set; }
    public string GeneratePath { get; set; } = "/generate-caption";
}
