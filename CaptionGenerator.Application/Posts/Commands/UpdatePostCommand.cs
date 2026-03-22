using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public record UpdatePostCommand(Guid Id, string caption, string imageUrl) : IRequest<bool>;

