namespace FinanzasPersonales.Api.Services;

// Sustituible e inyectable para incorporar autenticación en una fase posterior.
public interface IPasswordHasher
{
    string Hash(string password);
}
