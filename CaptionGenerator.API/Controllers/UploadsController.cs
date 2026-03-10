using CaptionGenerator.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaptionGenerator.API.Controllers;

[ApiController]
[Route("api/v1/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly IWebHostEnvironment _environment;

    public UploadsController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost("images")]
    [ProducesResponseType(typeof(ApiResponse<UploadImageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("File is required."));
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Only .jpg, .jpeg, .png and .webp are allowed."));
        }

        const long maxFileSizeBytes = 5 * 1024 * 1024;
        if (file.Length > maxFileSizeBytes)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("File size must be less than or equal to 5 MB."));
        }

        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadDirectory = Path.Combine(webRootPath, "uploads", "images");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadDirectory, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/images/{fileName}";
        var payload = new UploadImageResponse(imageUrl);

        return Ok(ApiResponse<UploadImageResponse>.SuccessResponse(payload, "Image uploaded successfully."));
    }
}

