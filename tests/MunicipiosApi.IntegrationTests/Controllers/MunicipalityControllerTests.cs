using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MunicipiosApi.Application.DTOs;
using MunicipiosApi.Domain.Interfaces;
using MunicipiosApi.Domain.Models;

namespace MunicipiosApi.IntegrationTests.Controllers;

public class MunicipalityControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MunicipalityControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithMockProvider(IEnumerable<Municipality>? municipalities = null, bool fail = false)
    {
        var providerMock = new Mock<IMunicipalityProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Mock");

        if (fail)
        {
            providerMock
                .Setup(p => p.GetByStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Municipality>>.Failure("Serviço externo indisponível"));
        }
        else
        {
            var data = municipalities ?? [new Municipality("Porto Alegre", "4314902")];
            providerMock
                .Setup(p => p.GetByStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(data));
        }

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMunicipalityProvider));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddScoped<IMunicipalityProvider>(_ => providerMock.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GET_municipalities_uf_ValidUf_Returns200WithData()
    {
        var municipalities = new List<Municipality>
        {
            new("Porto Alegre", "4314902"),
            new("Caxias do Sul", "4305108")
        };
        var client = CreateClientWithMockProvider(municipalities);

        var response = await client.GetAsync("/api/municipalities/RS");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<MunicipalityDto>>();
        body.Should().NotBeNull();
        body!.Data.Should().HaveCount(2);
        body.Total.Should().Be(2);
    }

    [Fact]
    public async Task GET_municipalities_uf_InvalidUf_Returns400()
    {
        var client = CreateClientWithMockProvider();

        var response = await client.GetAsync("/api/municipalities/XX");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_municipalities_uf_WithSearch_ReturnsFilteredResults()
    {
        var municipalities = new List<Municipality>
        {
            new("Porto Alegre", "4314902"),
            new("Porto Belo", "4214300"),
            new("Caxias do Sul", "4305108")
        };
        var client = CreateClientWithMockProvider(municipalities);

        var response = await client.GetAsync("/api/municipalities/RS?search=Porto");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<MunicipalityDto>>();
        body!.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GET_municipalities_uf_WithPagination_ReturnsCorrectPage()
    {
        var municipalities = Enumerable.Range(1, 20)
            .Select(i => new Municipality($"Cidade {i:D2}", $"43000{i:D2}"))
            .ToList();
        var client = CreateClientWithMockProvider(municipalities);

        var response = await client.GetAsync("/api/municipalities/RS?page=2&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<MunicipalityDto>>();
        body!.Data.Should().HaveCount(5);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(5);
        body.Total.Should().Be(20);
        body.TotalPages.Should().Be(4);
    }

    [Fact]
    public async Task GET_municipalities_uf_ProviderFails_Returns400WithError()
    {
        var client = CreateClientWithMockProvider(fail: true);

        var response = await client.GetAsync("/api/municipalities/RS");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_municipalities_uf_ResponseHasCorrectJsonFields()
    {
        var municipalities = new List<Municipality> { new("Porto Alegre", "4314902") };
        var client = CreateClientWithMockProvider(municipalities);

        var response = await client.GetAsync("/api/municipalities/RS");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("\"name\"");
        json.Should().Contain("\"ibge_code\"");
    }
}
