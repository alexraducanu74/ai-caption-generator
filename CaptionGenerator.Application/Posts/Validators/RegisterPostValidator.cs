using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Domain.Enums;
using FluentValidation;

namespace CaptionGenerator.Application.Posts.Validators;

public class RegisterPostValidator : AbstractValidator<RegisterPostCommand>
{
    public RegisterPostValidator()
    {
        RuleFor(p => p.caption).MaximumLength(100).NotEmpty();
        RuleFor(p => p.platform).IsInEnum();
        RuleFor(p => p.imageUrl)
            .NotEmpty()
            .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .WithMessage("Image URL must be a valid absolute URL.");
    }
}
