using MunicipiosApi.Application.DTOs;
using MunicipiosApi.Domain.Models;

namespace MunicipiosApi.Application.Interfaces;

public interface IMunicipalityService
{
    Task<Result<PagedResultDto<MunicipalityDto>>> GetByStateAsync(
        string uf,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);
}
