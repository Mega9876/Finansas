using Finanzas.Enums;
namespace Finanzas.Models;
public class Transaccion
{
    public Guid Id { get;  set; }
    public Guid UsuarioId { get;  set; }
    public Guid CuentaId { get;  set; }
    public Guid? CategoriaId { get;  set; }
    public TipoTransaccion Tipo { get;  set; }
    public decimal Monto { get;  set; }
    public DateTime FechaTransaccion { get;  set; }
    public DateTime FechaRegistro { get;  set; }
    public string Descripcion { get;  set; } = string.Empty;
    public string? Notas { get;  set; }
    public Guid? TransferenciaGrupoId { get;  set; }

    public Usuario? Usuario { get;  set; }
    public Cuenta? Cuenta { get;  set; }
    public Categoria? Categoria { get;  set; }

    
}