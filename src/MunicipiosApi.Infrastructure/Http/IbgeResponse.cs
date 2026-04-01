using System.Text.Json.Serialization;

namespace MunicipiosApi.Infrastructure.Http;

internal record IbgeMunicipio(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("nome")] string Nome
);
