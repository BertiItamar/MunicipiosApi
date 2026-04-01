using System.Net;
using System.Text.Json;
using MunicipiosApi.Api.ViewModels;

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
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();

        var message = env.IsDevelopment()
            ? ex.Message
            : "Ocorreu um erro interno. Por favor, tente novamente.";

        var body = new ErrorsViewModel([new ErrorViewModel(message)]);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
