using Finanzas.Enums;
namespace Finanzas.Models;
public class Categoria
{
    public Guid Id { get;  set; }
    public Guid? UsuarioId { get;  set; }
    public Guid? CategoriaPadreId { get;  set; }
    public string Nombre { get;  set; } = string.Empty;
    public TipoCategoria Tipo { get;  set; }
    public bool Activa { get;  set; }

    public Usuario? Usuario { get;  set; }
    public Categoria? CategoriaPadre { get;  set; }
    public IReadOnlyCollection<Categoria> Subcategorias => _subcategorias.AsReadOnly();

    private readonly List<Categoria> _subcategorias = [];

    
}