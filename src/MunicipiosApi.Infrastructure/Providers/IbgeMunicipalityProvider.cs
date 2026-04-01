using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using MunicipiosApi.Domain.Interfaces;
using MunicipiosApi.Domain.Models;
using MunicipiosApi.Infrastructure.Http;

namespace MunicipiosApi.Infrastructure.Providers;

public sealed class IbgeMunicipalityProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<IbgeMunicipalityProvider> logger) : IMunicipalityProvider
{
    public string ProviderName => "IBGE";

    public async Task<Result<IEnumerable<Municipality>>> GetByStateAsync(string uf, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Ibge");
            var response = await client.GetAsync($"api/v1/localidades/estados/{uf}/municipios", ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("IBGE retornou {StatusCode} para UF {Uf}", response.StatusCode, uf);
                return Result<IEnumerable<Municipality>>.Failure($"Erro ao consultar IBGE: {response.StatusCode}");
            }

            var municipios = await response.Content.ReadFromJsonAsync<IEnumerable<IbgeMunicipio>>(ct);

            if (municipios is null)
                return Result<IEnumerable<Municipality>>.Failure("IBGE retornou uma resposta vazia.");

            var result = municipios.Select(m => new Municipality(m.Nome, m.Id.ToString()));
            return Result<IEnumerable<Municipality>>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Falha de comunicação com IBGE para UF {Uf}", uf);
            return Result<IEnumerable<Municipality>>.Failure("Falha ao se comunicar com o serviço IBGE.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout ao consultar IBGE para UF {Uf}", uf);
            return Result<IEnumerable<Municipality>>.Failure("Timeout ao consultar o serviço IBGE.");
        }
    }
}
