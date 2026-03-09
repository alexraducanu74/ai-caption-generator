using System.ComponentModel.DataAnnotations;

namespace CaptionGenerator.API.Models.Requests;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password);
