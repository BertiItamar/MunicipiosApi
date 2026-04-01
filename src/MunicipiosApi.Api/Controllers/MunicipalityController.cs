using Microsoft.AspNetCore.Mvc;
using MunicipiosApi.Api.Extensions;
using MunicipiosApi.Api.ViewModels;
using MunicipiosApi.Application.DTOs;
using MunicipiosApi.Application.Interfaces;

namespace MunicipiosApi.Api.Controllers;

[ApiController]
[Route("api/municipalities")]
[Produces("application/json")]
public class MunicipalityController(IMunicipalityService service) : ControllerBase
{
    /// <summary>
    /// Lista os municípios de uma UF com suporte a paginação e busca.
    /// </summary>
    /// <param name="uf">Sigla do estado (ex: RS, SP, MG)</param>
    /// <param name="page">Número da página (default: 1)</param>
    /// <param name="pageSize">Itens por página, máximo 500 (default: 50)</param>
    /// <param name="search">Filtro por nome do município (opcional)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{uf}")]
    [ProducesResponseType<PagedResultDto<MunicipalityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorsViewModel>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByStateAsync(string uf,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await service.GetByStateAsync(uf, page, pageSize, search, ct);
        return result.ToActionResult();
    }
}
