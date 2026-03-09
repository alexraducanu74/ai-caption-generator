using System.ComponentModel.DataAnnotations;

namespace CaptionGenerator.API.Models.Requests;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password);
