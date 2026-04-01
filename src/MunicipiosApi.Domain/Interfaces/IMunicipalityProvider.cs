using MunicipiosApi.Domain.Models;

namespace MunicipiosApi.Domain.Interfaces;

public interface IMunicipalityProvider
{
    string ProviderName { get; }
    Task<Result<IEnumerable<Municipality>>> GetByStateAsync(string uf, CancellationToken ct = default);
}
