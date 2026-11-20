namespace Finanzas.Models;

public class Usuario
{
    public Guid Id { get;  set; }
    public string Email { get;  set; } = string.Empty;
    public string PasswordHash { get;  set; } = string.Empty;
    public string Nombre { get;  set; } = string.Empty;
    public string MonedaPrincipalId { get;  set; } = string.Empty;
    public DateTime FechaCreacion { get;  set; }
    public bool Activo { get;  set; }

    public Moneda? MonedaPrincipal { get;  set; }
    public IReadOnlyCollection<Categoria> Categorias => _categorias.AsReadOnly();
    public IReadOnlyCollection<Cuenta> Cuentas => _cuentas.AsReadOnly();
    public IReadOnlyCollection<Transaccion> Transacciones => _transacciones.AsReadOnly();

    private readonly List<Categoria> _categorias = [];
    private readonly List<Cuenta> _cuentas = [];
    private readonly List<Transaccion> _transacciones = [];

    
}