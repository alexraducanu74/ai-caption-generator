using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CaptionGenerator.API.Models.Requests;

public record RegisterRequest(
    [Required]
    [EmailAddress]
    string Email,
    [Required, MinLength(6)] string Password) : IValidatableObject
{
    private static readonly Regex StrictEmailRegex = new(
        @"^(?=.{1,254}$)(?=.{1,64}@)(?!.*\.\.)(?!\.)[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Email) && !StrictEmailRegex.IsMatch(Email))
        {
            yield return new ValidationResult(
                "Enter a valid email address, for example: name@example.com",
                [nameof(Email)]);
        }
    }
}
