using CaptionGenerator.API.Models;
using CaptionGenerator.API.Models.Requests;
using CaptionGenerator.API.Services;
using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Posts.Queries;
using CaptionGenerator.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaptionGenerator.API.Controllers;

[ApiController]
[Route("api/v1/posts")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICaptionGenerationClient _captionGenerationClient;
    private readonly IWebHostEnvironment _environment;

    public PostsController(IMediator mediator, ICaptionGenerationClient captionGenerationClient, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _captionGenerationClient = captionGenerationClient;
        _environment = environment;
    }

    /// <summary>
    /// Create a new post
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RegisterPost([FromBody] RegisterPostRequest request)
    {
        if (!Enum.IsDefined(typeof(SocialPlatform), request.Platform))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Invalid platform value."));
        }

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("ImageUrl is required."));
        }

        // Try to read the image bytes from disk so we can send them directly to the
        // caption service — this avoids cross-container URL-fetch failures entirely.
        var captionResult = await GenerateCaptionFromLocalFileOrUrlAsync(request.ImageUrl, HttpContext.RequestAborted);

        if (!captionResult.Success || string.IsNullOrWhiteSpace(captionResult.Caption))
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse<object>.ErrorResponse(captionResult.ErrorMessage ?? "Failed to generate caption."));
        }

        var command = new RegisterPostCommand(captionResult.Caption, (SocialPlatform)request.Platform, request.ImageUrl);
        var id = await _mediator.Send(command);
        var response = ApiResponse<Guid>.SuccessResponse(id, "Post created successfully");
        return CreatedAtAction(nameof(GetPostById), new { id }, response);
    }

    private async Task<(bool Success, string? Caption, string? ErrorMessage)> GenerateCaptionFromLocalFileOrUrlAsync(
        string imageUrl, CancellationToken cancellationToken)
    {
        // Extract just the path part of the URL (e.g. /uploads/images/abc.jpg)
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            // uri.AbsolutePath is like /uploads/images/abc.jpg
            var relativePath = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var localPath = Path.Combine(webRootPath, relativePath);

            if (System.IO.File.Exists(localPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(localPath, cancellationToken);
                var fileName = Path.GetFileName(localPath);
                return await _captionGenerationClient.GenerateCaptionFromBytesAsync(bytes, fileName, cancellationToken);
            }
        }

        // Fallback: pass URL (works in tests where stub is used, or when base URL is empty)
        return await _captionGenerationClient.GenerateCaptionAsync(imageUrl, cancellationToken);
    }

    /// <summary>
    /// Get all posts with optional filtering by status
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PostResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPosts([FromQuery] PostStatus? status)
    {
        var query = new GetPostQuery(status);
        var posts = await _mediator.Send(query);
        var response = posts
            .Select(p => new PostResponse(p.id, p.caption, p.platform, p.status, p.imageUrl, p.createdAtUtc))
            .ToList();
        return Ok(ApiResponse<List<PostResponse>>.SuccessResponse(response, "Posts retrieved successfully"));
    }

    /// <summary>
    /// Get a specific post by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostById(Guid id)
    {
        var query = new GetPostQuery(null);
        var posts = await _mediator.Send(query);
        var post = posts.FirstOrDefault(p => p.id == id);

        if (post == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Post not found"));
        }

        var response = new PostResponse(post.id, post.caption, post.platform, post.status, post.imageUrl, post.createdAtUtc);
        return Ok(ApiResponse<PostResponse>.SuccessResponse(response, "Post retrieved successfully"));
    }

    /// <summary>
    /// Delete a post by ID
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var command = new DeletePostCommand(id);
        var result = await _mediator.Send(command);

        if (!result)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Post not found"));
        }

        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Post deleted successfully"));
    }
}
