using GovernmentCollections.API.Extensions;
using GovernmentCollections.API.Middleware;
using GovernmentCollections.API;
using GovernmentCollections.Service.Services.InterswitchGovernmentCollections;
using Serilog;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog with console output like KeyLoyalty
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add application services
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddPaymentGatewayServices(builder.Configuration);
builder.Services.AddInterswitchServices(builder.Configuration);

// Add cache service (commented out until Redis is configured)
// builder.Services.AddScoped<GovernmentCollections.Service.Services.ICacheService, GovernmentCollections.Service.Services.RedisCacheService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));

// app.UseHttpsRedirection(); // Commented out for local development
app.UseCors("AllowAll");
app.UseExceptionHandler("/error");
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Log.Information("Starting Government Collections API");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
    if (addresses != null)
    {
        foreach (var address in addresses)
        {
            Log.Information("Now listening on: {Address}", address);
            Log.Information("Swagger UI available at: {Address}/swagger", address);
        }
    }
});

app.Run();