using Kahoot.Backend.Application.Authentication;
using Kahoot.Backend.Application.GameSessions;
using Kahoot.Backend.Infrastructure.Authentication;
using Kahoot.Backend.Infrastructure.GameSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Kahoot.Backend.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException("Connection string 'PostgreSql' is missing.");

        services.AddDbContext<KahootDbContext>(options =>
            options.UseNpgsql(connectionString));

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' is missing.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IPlayerAuthService, PlayerAuthService>();
        services.AddSingleton<IGamePinGenerator, GamePinGenerator>();
        services.AddScoped<IGameSessionStore, RedisGameSessionStore>();
        services.AddScoped<IGameFlowService, GameFlowService>();
        services.AddHostedService<AdminSeedService>();

        return services;
    }
}
