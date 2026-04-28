using FplLiveRank.Api.Middleware;
using FplLiveRank.Application;
using FplLiveRank.Infrastructure;
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

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                         ["http://localhost:4200"])
            .AllowAnyHeader()
            .AllowAnyMethod()));

    var app = builder.Build();

    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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

 
