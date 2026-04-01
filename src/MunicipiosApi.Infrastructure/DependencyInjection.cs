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
        services.AddMemoryCache();

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

        var providerEnv = configuration["MUNICIPALITY_PROVIDER"] ?? "BrasilApi";

        if (Enum.TryParse<ProviderEnum>(providerEnv, ignoreCase: true, out var selectedProvider) && selectedProvider == ProviderEnum.Ibge)
            services.AddScoped<IMunicipalityProvider, IbgeMunicipalityProvider>();
        else
            services.AddScoped<IMunicipalityProvider, BrasilApiMunicipalityProvider>();

        services.AddScoped<IMunicipalityService, MunicipalityService>();

        return services;
    }
}
