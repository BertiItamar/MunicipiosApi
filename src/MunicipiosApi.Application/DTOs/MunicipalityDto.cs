using System.Text.Json.Serialization;

namespace MunicipiosApi.Application.DTOs;

public record MunicipalityDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ibge_code")] string IbgeCode
);
