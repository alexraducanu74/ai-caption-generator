using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Domain.Enums;
using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public sealed class ConfirmPostPublicationCommandHandler : IRequestHandler<ConfirmPostPublicationCommand, bool>
{
    private readonly IPostsRepository _repository;

    public ConfirmPostPublicationCommandHandler(IPostsRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ConfirmPostPublicationCommand request, CancellationToken cancellationToken)
    {
        var post = await _repository.FindAsync(request.Id, cancellationToken);
        if (post is null || post.Status != PostStatus.PendingConfirmation)
        {
            return false;
        }

        post.MarkAsPublished();
        await _repository.UpdateAsync(post, cancellationToken);
        return true;
    }
}

