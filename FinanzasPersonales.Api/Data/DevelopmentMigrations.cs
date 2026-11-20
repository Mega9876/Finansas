using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanzasPersonales.Api.Data;

public static class DevelopmentMigrations
{
    public static async Task ApplyDevelopmentMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
                return;
            }
            catch (Exception ex) when (attempt < 15 &&
                (ex is NpgsqlException { IsTransient: true } || ex is TimeoutException))
            {
                app.Logger.LogWarning("PostgreSQL no disponible; reintento de migraciones {Attempt}/15.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), app.Lifetime.ApplicationStopping);
            }
        }
    }
}
