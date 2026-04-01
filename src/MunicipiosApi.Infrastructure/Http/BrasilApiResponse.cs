using System.Text.Json.Serialization;

namespace MunicipiosApi.Infrastructure.Http;

internal record BrasilApiMunicipio(
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("codigo_ibge")] string CodigoIbge
);
