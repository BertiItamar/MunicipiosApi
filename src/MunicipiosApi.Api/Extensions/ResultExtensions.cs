using Microsoft.AspNetCore.Mvc;
using MunicipiosApi.Api.ViewModels;
using MunicipiosApi.Domain.Models;

namespace MunicipiosApi.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsFailure)
            return new BadRequestObjectResult(new ErrorsViewModel(
                result.Errors.Select(e => new ErrorViewModel(e)).ToList()));

        return new OkObjectResult(result.Value);
    }
}
