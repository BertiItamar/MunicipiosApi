namespace MunicipiosApi.Application.DTOs;

public record PagedResultDto<T>(
    IEnumerable<T> Data,
    int Page,
    int PageSize,
    int Total,
    int TotalPages
);
