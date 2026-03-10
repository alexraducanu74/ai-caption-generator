using CaptionGenerator.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaptionGenerator.Application.Posts.Queries;

public class GetPostCommandHandler : IRequestHandler<GetPostQuery, List<PostDto>>
{
    private readonly IPostsRepository _repository;

    public GetPostCommandHandler(IPostsRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PostDto>> Handle(GetPostQuery request, CancellationToken cancellationToken)
    {
        var query = _repository.Query();

        if (request.Status is not null)
        {
            query = query.Where(p => p.Status == request.Status);
        }

        var posts = await query.ToListAsync(cancellationToken);
        return posts
            .Select(p => new PostDto(p.Id, p.Caption, p.Platform.ToString(), p.Status.ToString(), p.ImageUrl, p.CreatedAtUtc))
            .ToList();
    }
}
