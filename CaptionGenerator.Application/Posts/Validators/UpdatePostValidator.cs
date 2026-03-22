using CaptionGenerator.Application.Posts.Commands;
using FluentValidation;

namespace CaptionGenerator.Application.Posts.Validators;

public class UpdatePostValidator : AbstractValidator<UpdatePostCommand>
{
    private const int MaxCaptionLength = 280;

    public UpdatePostValidator()
    {
        RuleFor(p => p.caption).MaximumLength(MaxCaptionLength).NotEmpty();
        RuleFor(p => p.imageUrl)
            .NotEmpty()
            .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .WithMessage("Image URL must be a valid absolute URL.");
    }
}

