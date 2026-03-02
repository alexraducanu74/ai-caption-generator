using CaptionGenerator.Domain.Enums;
using MediatR;

namespace CaptionGenerator.Application.Posts.Queries;

public record GetPostQuery(PostStatus? Status) : IRequest<List<PostDto>>
{
    
}