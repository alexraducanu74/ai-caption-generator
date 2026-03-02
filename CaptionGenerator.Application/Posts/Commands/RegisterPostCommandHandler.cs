using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Domain.Entities;
using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public sealed class RegisterPostCommandHandler : IRequestHandler<RegisterPostCommand, Guid>
{
    private readonly IPostsRepository _repository;
    
    public RegisterPostCommandHandler(IPostsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(RegisterPostCommand request, CancellationToken cancellationToken)
    {
        var post = Post.Create(request.caption, request.platform);
        await _repository.AddAsync(post, cancellationToken);
        return post.Id;
    }
}