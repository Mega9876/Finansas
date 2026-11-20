using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FinanzasPersonales.Api.DTOs;

public sealed class CrearUsuarioDto : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Nombre { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
            yield return new ValidationResult("El nombre no puede estar vacío.", [nameof(Nombre)]);

        // BCrypt admite como máximo 72 bytes, no 72 caracteres Unicode.
        if (Password is not null && (string.IsNullOrWhiteSpace(Password) ||
            Encoding.UTF8.GetByteCount(Password) > 72 || Password.Contains('\0')))
            yield return new ValidationResult("La contraseña debe tener entre 12 y 72 caracteres, no superar 72 bytes UTF-8 y no contener NUL.", [nameof(Password)]);
    }
}
