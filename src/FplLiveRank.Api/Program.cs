using FplLiveRank.Api.Hubs;
using FplLiveRank.Api.Middleware;
using FplLiveRank.Application;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Jobs;
using FplLiveRank.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    // Replace the Application-default null broadcaster with the SignalR-backed one.
    builder.Services.RemoveAll<IFplLiveBroadcaster>();
    builder.Services.AddSingleton<IFplLiveBroadcaster, SignalRFplLiveBroadcaster>();

    builder.Services.AddSignalR();

    var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=fpllive;Username=fpllive;Password=fpllive";

    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(postgresConnectionString, _ => { })));
    builder.Services.AddHangfireServer();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();

    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                         ["http://localhost:4200"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

    var app = builder.Build();

    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "FPL Live Rank v1");
            options.DocumentTitle = "FPL Live Rank API";
            options.RoutePrefix = "swagger";
        });
    }

    app.MapControllers();
    app.MapHub<FplLiveHub>(FplLiveHub.Path);
    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

    // Schedule the event-live refresh recurring job. The cron expression below is
    // every 60 seconds — adequate for live match days; it's also harmless out of season
    // since the job swallows exceptions.
    var recurring = app.Services.GetRequiredService<IRecurringJobManager>();
    recurring.AddOrUpdate<EventLiveRefreshJob>(
        EventLiveRefreshJob.RecurringJobId,
        job => job.RunAsync(),
        Cron.Minutely);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
