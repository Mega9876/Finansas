// Finanzas/Data/AppDbContext.cs
using Finanzas.Models;
using Microsoft.EntityFrameworkCore;

namespace Finanzas.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Tu tabla
        public DbSet<Moneda> Monedas { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cuenta> Cuentas { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Transaccion> Transacciones { get; set; }


        
    }
}