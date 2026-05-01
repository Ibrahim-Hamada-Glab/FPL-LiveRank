using FplLiveRank.Application.Interfaces;
using FplLiveRank.Infrastructure.Cache;
using FplLiveRank.Infrastructure.External.Fpl;
using FplLiveRank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;

namespace FplLiveRank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        AddPersistence(services, config);
        AddCache(services, config);
        AddFplClient(services, config);
        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=fpllive;Username=fpllive;Password=fpllive";

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
    }

    private static void AddCache(IServiceCollection services, IConfiguration config)
    {
        services.Configure<RedisCacheOptions>(config.GetSection(RedisCacheOptions.SectionName));
        var redisOptions = config.GetSection(RedisCacheOptions.SectionName).Get<RedisCacheOptions>() ?? new RedisCacheOptions();

        if (redisOptions.Enabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, NullCacheService>();
        }
    }

    private static void AddFplClient(IServiceCollection services, IConfiguration config)
    {
        services.Configure<FplApiOptions>(config.GetSection(FplApiOptions.SectionName));
        var fplOptions = config.GetSection(FplApiOptions.SectionName).Get<FplApiOptions>() ?? new FplApiOptions();

        services.AddHttpClient<IFplApiClient, FplApiClient>(FplApiClient.HttpClientName, http =>
        {
            http.BaseAddress = new Uri(fplOptions.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(fplOptions.TimeoutSeconds);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(fplOptions.UserAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .AddPolicyHandler(BuildRetryPolicy(fplOptions.RetryCount));
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200));

}
