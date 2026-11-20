using Finanzas.Enums;
namespace Finanzas.Models;
public class Cuenta
{
    public Guid Id { get;  set; }
    public Guid UsuarioId { get;  set; }
    public string MonedaId { get;  set; } = string.Empty;
    public string Nombre { get;  set; } = string.Empty;
    public TipoCuenta Tipo { get;  set; }
    public decimal SaldoInicial { get;  set; }
    public decimal SaldoActual { get;  set; }
    public bool Activa { get;  set; }

    public Usuario? Usuario { get;  set; }
    public Moneda? Moneda { get;  set; }
    public IReadOnlyCollection<Transaccion> Transacciones => _transacciones.AsReadOnly();

    private readonly List<Transaccion> _transacciones = [];

    
}