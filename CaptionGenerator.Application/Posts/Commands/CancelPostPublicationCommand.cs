using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public record CancelPostPublicationCommand(Guid Id) : IRequest<bool>;

