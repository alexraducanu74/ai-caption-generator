using CaptionGenerator.Application.Posts.Commands;
using FluentValidation;

namespace CaptionGenerator.Application.Posts.Validators;

public class RegisterPostValidator : AbstractValidator<RegisterPostCommand>
{
    private const int MaxCaptionLength = 280;

    public RegisterPostValidator()
    {
        RuleFor(p => p.caption).MaximumLength(MaxCaptionLength).NotEmpty();
        RuleFor(p => p.platform).IsInEnum();
        RuleFor(p => p.imageUrl)
            .NotEmpty()
            .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .WithMessage("Image URL must be a valid absolute URL.");
    }
}
