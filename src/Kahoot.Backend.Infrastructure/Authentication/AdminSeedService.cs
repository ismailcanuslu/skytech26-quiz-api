using Kahoot.Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kahoot.Backend.Infrastructure.Authentication;

internal sealed class AdminSeedService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<AdminSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KahootDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Admin seed skipped because AdminSeed settings are missing.");
            return;
        }

        var adminExists = await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken);
        if (adminExists)
        {
            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed admin user has been created for {Email}.", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
