using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public record DeletePostCommand(Guid Id) : IRequest<bool>;

