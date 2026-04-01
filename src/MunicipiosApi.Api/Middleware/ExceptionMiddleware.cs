using System.Net;
using System.Text.Json;

namespace MunicipiosApi.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Requisição cancelada pelo cliente — sem log de erro
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exceção não tratada: {Message}", ex.Message);
            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var isDevelopment = context.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment();

        var body = JsonSerializer.Serialize(new
        {
            errors = new[] { isDevelopment ? ex.Message : "Ocorreu um erro interno. Por favor, tente novamente." }
        });

        await context.Response.WriteAsync(body);
    }
}
