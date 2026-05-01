using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Jobs;
using FplLiveRank.Application.Services;
using FplLiveRank.Application.Calculators;
using Microsoft.Extensions.DependencyInjection;

namespace FplLiveRank.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IFplBootstrapService, FplBootstrapService>();
        services.AddScoped<IManagerLeaguesService, ManagerLeaguesService>();
        services.AddScoped<IManagerLiveScoreService, ManagerLiveScoreService>();
        services.AddScoped<ILeagueLiveRankService, LeagueLiveRankService>();
        services.AddSingleton<IEffectiveOwnershipCalculator, EffectiveOwnershipCalculator>();
        services.AddScoped<ILeagueEffectiveOwnershipService, LeagueEffectiveOwnershipService>();
        // Default broadcaster is a no-op so Application services can depend on it
        // even when SignalR isn't wired (e.g., unit tests). The Api project replaces
        // this with a SignalR-backed implementation in its DI.
        services.AddSingleton<IFplLiveBroadcaster, NullFplLiveBroadcaster>();
        services.AddScoped<EventLiveRefreshJob>();
        return services;
    }
}
