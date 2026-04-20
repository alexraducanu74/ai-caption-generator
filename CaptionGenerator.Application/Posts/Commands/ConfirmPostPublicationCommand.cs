using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public record ConfirmPostPublicationCommand(Guid Id) : IRequest<bool>;

