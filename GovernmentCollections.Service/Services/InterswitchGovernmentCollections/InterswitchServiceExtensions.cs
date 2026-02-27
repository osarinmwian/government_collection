using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using GovernmentCollections.Domain.Settings;
using GovernmentCollections.Shared.Validation;
using GovernmentCollections.Service.Services.Settlement;

namespace GovernmentCollections.Service.Services.InterswitchGovernmentCollections;

public static class InterswitchServiceExtensions
{
    public static IServiceCollection AddInterswitchServices(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("InterswitchSettings").Get<InterswitchSettings>() ?? new InterswitchSettings();
        services.AddSingleton(settings);

        services.AddHttpClient<InterswitchAuthService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        
        services.AddHttpClient<InterswitchTransactionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        
        services.AddHttpClient<BillPayment.InterswitchBillPaymentService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddHttpClient<SettlementService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes timeout
        });
        
        services.AddScoped<IPinValidationService, PinValidationService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IInterswitchGovernmentCollectionsService, InterswitchGovernmentCollectionsService>();

        return services;
    }
}