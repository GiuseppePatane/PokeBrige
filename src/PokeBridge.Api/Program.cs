using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PokeBridge.Infrastructure.EF;
using PokeBridge.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi


builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddLogging();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHostedService<MigrationHostedService>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PokeBridge API",
        Version = "v1",
        Description = "PokeBridge API for Pokemon information with fun translations"
    });
    
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Production System API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Production System API";
        options.DisplayRequestDuration();
    });
}
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.Run();

public partial class Program { }

public class MigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MigrationHostedService> _logger;

    public MigrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<MigrationHostedService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PokeBridgeDbContext>();

            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                var migrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                if (migrations.Any())
                {
                    _logger.LogInformation("Applying database migrations...");
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Database migrations applied successfully.");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while applying database migrations.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
