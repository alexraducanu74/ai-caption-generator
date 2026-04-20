using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Domain.Enums;
using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public sealed class CancelPostPublicationCommandHandler : IRequestHandler<CancelPostPublicationCommand, bool>
{
    private readonly IPostsRepository _repository;

    public CancelPostPublicationCommandHandler(IPostsRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(CancelPostPublicationCommand request, CancellationToken cancellationToken)
    {
        var post = await _repository.FindAsync(request.Id, cancellationToken);
        if (post is null || post.Status != PostStatus.PendingConfirmation)
        {
            return false;
        }

        post.MarkAsCanceled();
        await _repository.UpdateAsync(post, cancellationToken);
        return true;
    }
}

