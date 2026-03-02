using CaptionGenerator.Application.Abstractions;
using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public sealed class DeletePostCommandHandler : IRequestHandler<DeletePostCommand, bool>
{
    private readonly IPostsRepository _repository;

    public DeletePostCommandHandler(IPostsRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _repository.FindAsync(request.Id, cancellationToken);

        if (post == null)
        {
            return false;
        }

        await _repository.DeleteAsync(post, cancellationToken);
        return true;
    }
}

