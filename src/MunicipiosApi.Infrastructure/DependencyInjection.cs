using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MunicipiosApi.Application.Interfaces;
using MunicipiosApi.Application.Services;
using MunicipiosApi.Domain.Enums;
using MunicipiosApi.Domain.Interfaces;
using MunicipiosApi.Infrastructure.Providers;

namespace MunicipiosApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddCache(services, configuration);
        AddHttpClients(services);
        AddProviders(services, configuration);

        services.AddScoped<IMunicipalityService, MunicipalityService>();

        return services;
    }

    private static void AddCache(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "municipios:";
            });
        }
        else
        {
            // Fallback para in-memory quando Redis não está configurado (ex: desenvolvimento local sem Docker)
            services.AddDistributedMemoryCache();
        }
    }

    private static void AddHttpClients(IServiceCollection services)
    {
        services.AddHttpClient("BrasilApi", client =>
        {
            client.BaseAddress = new Uri("https://brasilapi.com.br/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("Ibge", client =>
        {
            client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private static void AddProviders(IServiceCollection services, IConfiguration configuration)
    {
        var providerEnv = configuration["MUNICIPALITY_PROVIDER"] ?? "BrasilApi";

        if (Enum.TryParse<ProviderEnum>(providerEnv, ignoreCase: true, out var selected) && selected == ProviderEnum.Ibge)
            services.AddScoped<IMunicipalityProvider, IbgeMunicipalityProvider>();
        else
            services.AddScoped<IMunicipalityProvider, BrasilApiMunicipalityProvider>();
    }
}
