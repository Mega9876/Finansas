namespace Finanzas.Models;

public class Moneda
{
    public string Id { get;  set; } = string.Empty; // ISO 4217: "BOB", "USD"
    public string Nombre { get;  set; } = string.Empty;
    public string Simbolo { get;  set; } = string.Empty;

    
}