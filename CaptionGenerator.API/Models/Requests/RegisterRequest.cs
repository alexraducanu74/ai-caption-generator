using System.ComponentModel.DataAnnotations;

namespace CaptionGenerator.API.Models.Requests;

public record RegisterRequest(
    [Required, EmailAddress, RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", ErrorMessage = "Email must include a valid domain, for example: name@example.com")] string Email,
    [Required, MinLength(6)] string Password);
