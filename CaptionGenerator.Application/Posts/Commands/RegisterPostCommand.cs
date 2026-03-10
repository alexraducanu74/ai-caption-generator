﻿using CaptionGenerator.Domain.Enums;
using MediatR;

namespace CaptionGenerator.Application.Posts.Commands;

public record RegisterPostCommand(string caption, SocialPlatform platform, string imageUrl) : IRequest<Guid>
{

}
