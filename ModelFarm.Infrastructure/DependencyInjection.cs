using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelFarm.Infrastructure.MarketData;
using ModelFarm.Infrastructure.Persistence;

namespace ModelFarm.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionString = "Host=localhost;Database=modelfarm;Username=postgres;Password=123456";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register DbContexts with factory support for background tasks
        services.AddDbContextFactory<DataDbContext>(options =>
            options.UseNpgsql(ConnectionString)
                   .UseSnakeCaseNamingConvention());
        
        services.AddDbContextFactory<IdentityDbContext>(options =>
            options.UseNpgsql(ConnectionString)
                   .UseSnakeCaseNamingConvention());
        
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(ConnectionString)
                   .UseSnakeCaseNamingConvention());

        // Register HTTP client for Binance
        services.AddHttpClient<BinanceMarketDataProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.binance.com");
            client.DefaultRequestHeaders.Add("User-Agent", "ModelFarm/1.0");
        });

        // Register market data provider factory
        services.AddScoped<IMarketDataProviderFactory, MarketDataProviderFactory>();

        return services;
    }
}
