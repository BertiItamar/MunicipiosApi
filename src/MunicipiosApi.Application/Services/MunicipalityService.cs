using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MunicipiosApi.Application.DTOs;
using MunicipiosApi.Application.Interfaces;
using MunicipiosApi.Domain.Interfaces;
using MunicipiosApi.Domain.Models;

namespace MunicipiosApi.Application.Services;

public sealed class MunicipalityService(
    IMunicipalityProvider provider,
    IDistributedCache cache,
    ILogger<MunicipalityService> logger) : IMunicipalityService
{
    private static readonly HashSet<string> ValidUfs =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO",
        "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI",
        "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
    };

    public async Task<Result<PagedResultDto<MunicipalityDto>>> GetByStateAsync(
        string uf,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        var normalizedUf = uf.Trim().ToUpperInvariant();

        if (!ValidUfs.Contains(normalizedUf))
            return Result<PagedResultDto<MunicipalityDto>>.Failure($"UF '{uf}' inválida. Use a sigla de um dos 27 estados brasileiros.");

        if (page < 1)
            return Result<PagedResultDto<MunicipalityDto>>.Failure("O número da página deve ser maior ou igual a 1.");

        if (pageSize < 1 || pageSize > 500)
            return Result<PagedResultDto<MunicipalityDto>>.Failure("O tamanho da página deve estar entre 1 e 500.");

        var municipalities = await GetFromCacheOrProviderAsync(normalizedUf, ct);

        if (municipalities is null)
            return Result<PagedResultDto<MunicipalityDto>>.Failure("Falha ao obter dados do provider.");

        if (municipalities.Count == 0 && !string.IsNullOrWhiteSpace(search) is false)
            return Result<PagedResultDto<MunicipalityDto>>.Failure($"Nenhum município encontrado para a UF '{normalizedUf}'.");

        IEnumerable<Municipality> filtered = municipalities;

        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(m => m.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

        var dtos = filtered
            .OrderBy(m => m.Name)
            .Select(m => new MunicipalityDto(m.Name, m.IbgeCode))
            .ToList();

        var total = dtos.Count;
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        var paged = dtos.Skip((page - 1) * pageSize).Take(pageSize);

        return Result<PagedResultDto<MunicipalityDto>>.Success(
            new PagedResultDto<MunicipalityDto>(paged, page, pageSize, total, totalPages));
    }

    private async Task<List<Municipality>?> GetFromCacheOrProviderAsync(string uf, CancellationToken ct)
    {
        var cacheKey = $"municipios:{uf}";

        try
        {
            var cached = await cache.GetStringAsync(cacheKey, ct);

            if (cached is not null)
            {
                logger.LogInformation("Cache hit para UF {Uf}", uf);
                return JsonSerializer.Deserialize<List<Municipality>>(cached);
            }
        }
        catch (Exception ex)
        {
            // Redis indisponível — segue sem cache
            logger.LogWarning(ex, "Cache indisponível para UF {Uf}. Consultando provider diretamente.", uf);
        }

        logger.LogInformation("Cache miss para UF {Uf} — consultando provider {Provider}", uf, provider.ProviderName);

        var result = await provider.GetByStateAsync(uf, ct);

        if (result.IsFailure)
            return null;

        var municipalities = result.Value!.ToList();

        try
        {
            var serialized = JsonSerializer.Serialize(municipalities);
            await cache.SetStringAsync(cacheKey, serialized, CacheOptions, ct);
            logger.LogInformation("{Count} municípios de {Uf} armazenados em cache por 24h", municipalities.Count, uf);
        }
        catch (Exception ex)
        {
            // Falha ao gravar cache não deve quebrar a resposta
            logger.LogWarning(ex, "Falha ao gravar cache para UF {Uf}", uf);
        }

        return municipalities;
    }
}
