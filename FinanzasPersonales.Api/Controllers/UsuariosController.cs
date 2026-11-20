using FinanzasPersonales.Api.Data;
using FinanzasPersonales.Api.DTOs;
using FinanzasPersonales.Api.Models;
using FinanzasPersonales.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public sealed class UsuariosController(AppDbContext db, IPasswordHasher passwordHasher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<UsuarioRespuestaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UsuarioRespuestaDto>>> Listar(CancellationToken cancellationToken)
    {
        var usuarios = await db.Usuarios.AsNoTracking()
            .Where(u => u.Activo)
            .OrderBy(u => u.FechaCreacion).ThenBy(u => u.Id)
            .Select(u => new UsuarioRespuestaDto(u.Id, u.Nombre, u.Email, u.FechaCreacion))
            .ToListAsync(cancellationToken);
        return Ok(usuarios);
    }

    [HttpPost]
    [ProducesResponseType<UsuarioRespuestaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioRespuestaDto>> Crear(CrearUsuarioDto dto, CancellationToken cancellationToken)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Email == email, cancellationToken))
            return CorreoDuplicado();

        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(dto.Password),
            Activo = true
        };
        db.Usuarios.Add(usuario);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_usuario_email" })
        {
            // Otra petición pudo registrar el mismo correo tras la consulta previa.
            return CorreoDuplicado();
        }

        // Location apunta a la colección: no existe un cuarto endpoint GET por id.
        return Created("/api/usuarios", new UsuarioRespuestaDto(
            usuario.Id, usuario.Nombre, usuario.Email, usuario.FechaCreacion));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (usuario is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Usuario no encontrado.");

        // Idempotente: un usuario ya desactivado también devuelve 204.
        if (!usuario.Activo)
            return NoContent();

        usuario.Activo = false;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private ObjectResult CorreoDuplicado() => Problem(
        statusCode: StatusCodes.Status409Conflict, title: "El correo electrónico ya está registrado.");
}
