using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MunicipiosApi.Application.DTOs;
using MunicipiosApi.Application.Interfaces;
using MunicipiosApi.Domain.Interfaces;
using MunicipiosApi.Domain.Models;

namespace MunicipiosApi.Application.Services;

public sealed class MunicipalityService(
    IMunicipalityProvider provider,
    IMemoryCache cache,
    ILogger<MunicipalityService> logger) : IMunicipalityService
{
    private static readonly HashSet<string> ValidUfs =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO",
        "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI",
        "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

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

        var cacheKey = $"municipios:{normalizedUf}";

        if (!cache.TryGetValue(cacheKey, out IEnumerable<Municipality>? municipalities))
        {
            logger.LogInformation("Cache miss para UF {Uf} usando provider {Provider}", normalizedUf, provider.ProviderName);

            var result = await provider.GetByStateAsync(normalizedUf, ct);

            if (result.IsFailure)
                return Result<PagedResultDto<MunicipalityDto>>.Failure(result.Errors);

            municipalities = result.Value!;

            cache.Set(cacheKey, municipalities, TimeSpan.FromHours(24));
            logger.LogInformation("{Count} municípios de {Uf} armazenados em cache por 24h", municipalities.Count(), normalizedUf);
        }
        else
        {
            logger.LogInformation("Cache hit para UF {Uf}", normalizedUf);
        }

        if (!string.IsNullOrWhiteSpace(search))
            municipalities = municipalities!.Where(m => m.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

        var dtos = municipalities!
            .OrderBy(m => m.Name)
            .Select(m => new MunicipalityDto(m.Name, m.IbgeCode))
            .ToList();

        var total = dtos.Count;
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        var paged = dtos.Skip((page - 1) * pageSize).Take(pageSize);

        return Result<PagedResultDto<MunicipalityDto>>.Success(
            new PagedResultDto<MunicipalityDto>(paged, page, pageSize, total, totalPages));
    }
}
