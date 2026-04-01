using MunicipiosApi.Api.Middleware;
using MunicipiosApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Municípios API",
        Version = "v1",
        Description = "API para consulta de municípios brasileiros por UF. " +
                      "O provider de dados (BrasilAPI ou IBGE) é configurado via variável de ambiente MUNICIPALITY_PROVIDER."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Municípios API v1"));
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program;
