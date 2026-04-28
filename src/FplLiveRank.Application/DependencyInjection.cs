using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FplLiveRank.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IFplBootstrapService, FplBootstrapService>();
        services.AddScoped<IManagerLiveScoreService, ManagerLiveScoreService>();
        services.AddScoped<ILeagueLiveRankService, LeagueLiveRankService>();
        return services;
    }
}
