using FinanzasPersonales.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanzasPersonales.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var usuario = modelBuilder.Entity<Usuario>();
        usuario.ToTable("usuario", table => table.HasCheckConstraint(
            "ck_usuario_email_normalizado", "email = lower(btrim(email))"));
        usuario.HasKey(u => u.Id).HasName("pk_usuario");
        usuario.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
        usuario.Property(u => u.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        usuario.Property(u => u.Email).HasColumnName("email").HasMaxLength(150).IsRequired();
        usuario.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_usuario_email");
        usuario.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        usuario.Property(u => u.FechaCreacion).HasColumnName("fecha_creacion")
            .HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        usuario.Property(u => u.Activo).HasColumnName("activo").HasDefaultValue(true);
    }
}
