using CaptionGenerator.Application.Abstractions;
using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public sealed class UpdatePostCommandHandler : IRequestHandler<UpdatePostCommand, bool>
{
    private readonly IPostsRepository _repository;

    public UpdatePostCommandHandler(IPostsRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _repository.FindAsync(request.Id, cancellationToken);
        if (post is null)
        {
            return false;
        }

        post.UpdateDetails(request.caption, request.imageUrl);
        await _repository.UpdateAsync(post, cancellationToken);
        return true;
    }
}

