using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using MunicipiosApi.Domain.Interfaces;
using MunicipiosApi.Domain.Models;
using MunicipiosApi.Infrastructure.Http;

namespace MunicipiosApi.Infrastructure.Providers;

public sealed class BrasilApiMunicipalityProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<BrasilApiMunicipalityProvider> logger) : IMunicipalityProvider
{
    public string ProviderName => "BrasilAPI";

    public async Task<Result<IEnumerable<Municipality>>> GetByStateAsync(string uf, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("BrasilApi");
            var response = await client.GetAsync($"api/ibge/municipios/v1/{uf}", ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("BrasilAPI retornou {StatusCode} para UF {Uf}", response.StatusCode, uf);
                return Result<IEnumerable<Municipality>>.Failure($"Erro ao consultar BrasilAPI: {response.StatusCode}");
            }

            var municipios = await response.Content.ReadFromJsonAsync<IEnumerable<BrasilApiMunicipio>>(ct);

            if (municipios is null)
                return Result<IEnumerable<Municipality>>.Failure("BrasilAPI retornou uma resposta vazia.");

            var result = municipios.Select(m => new Municipality(m.Nome, m.CodigoIbge));
            return Result<IEnumerable<Municipality>>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Falha de comunicação com BrasilAPI para UF {Uf}", uf);
            return Result<IEnumerable<Municipality>>.Failure("Falha ao se comunicar com o serviço BrasilAPI.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout ao consultar BrasilAPI para UF {Uf}", uf);
            return Result<IEnumerable<Municipality>>.Failure("Timeout ao consultar o serviço BrasilAPI.");
        }
    }
}
