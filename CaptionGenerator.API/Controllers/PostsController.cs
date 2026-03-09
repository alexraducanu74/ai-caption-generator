using CaptionGenerator.API.Models;
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

    public PostsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Create a new post
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterPost([FromBody] RegisterPostRequest request)
    {
        var command = new RegisterPostCommand(request.Caption, (SocialPlatform)request.Platform);
        var id = await _mediator.Send(command);
        var response = ApiResponse<Guid>.SuccessResponse(id, "Post created successfully");
        return CreatedAtAction(nameof(GetPostById), new { id }, response);
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
        var response = posts.Select(p => new PostResponse(p.id, p.caption, p.platform, p.status)).ToList();
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

        var response = new PostResponse(post.id, post.caption, post.platform, post.status);
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
