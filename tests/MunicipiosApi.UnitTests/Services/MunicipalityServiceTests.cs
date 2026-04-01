using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MunicipiosApi.Application.Services;
using MunicipiosApi.Domain.Interfaces;
using MunicipiosApi.Domain.Models;

namespace MunicipiosApi.UnitTests.Services;

public class MunicipalityServiceTests
{
    private readonly Mock<IMunicipalityProvider> _providerMock;
    private readonly IMemoryCache _cache;
    private readonly MunicipalityService _sut;

    public MunicipalityServiceTests()
    {
        _providerMock = new Mock<IMunicipalityProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new MunicipalityService(
            _providerMock.Object,
            _cache,
            NullLogger<MunicipalityService>.Instance);
    }

    [Fact]
    public async Task GetByStateAsync_ValidUf_ReturnsPagedMunicipalities()
    {
        var municipalities = new List<Municipality>
        {
            new("Porto Alegre", "4314902"),
            new("Caxias do Sul", "4305108"),
            new("Pelotas", "4314407")
        };

        _providerMock
            .Setup(p => p.GetByStateAsync("RS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(municipalities));

        var result = await _sut.GetByStateAsync("RS", 1, 10, null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(3);
        result.Value.Total.Should().Be(3);
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetByStateAsync_InvalidUf_ReturnsFailure()
    {
        var result = await _sut.GetByStateAsync("XX", 1, 10, null);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Contains("UF") && e.Contains("inválida"));
        _providerMock.Verify(p => p.GetByStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByStateAsync_InvalidPage_ReturnsFailure(int page)
    {
        var result = await _sut.GetByStateAsync("RS", page, 10, null);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Contains("página"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task GetByStateAsync_InvalidPageSize_ReturnsFailure(int pageSize)
    {
        var result = await _sut.GetByStateAsync("RS", 1, pageSize, null);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Contains("tamanho da página"));
    }

    [Fact]
    public async Task GetByStateAsync_WithSearch_FiltersResults()
    {
        var municipalities = new List<Municipality>
        {
            new("Porto Alegre", "4314902"),
            new("Porto Belo", "4214300"),
            new("Caxias do Sul", "4305108")
        };

        _providerMock
            .Setup(p => p.GetByStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(municipalities));

        var result = await _sut.GetByStateAsync("RS", 1, 10, "Porto");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(2);
        result.Value.Data.Should().AllSatisfy(m => m.Name.Should().Contain("Porto"));
    }

    [Fact]
    public async Task GetByStateAsync_UfIsCaseInsensitive_NormalizesToUppercase()
    {
        var municipalities = new List<Municipality> { new("Porto Alegre", "4314902") };

        _providerMock
            .Setup(p => p.GetByStateAsync("RS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(municipalities));

        var result = await _sut.GetByStateAsync("rs", 1, 10, null);

        result.IsSuccess.Should().BeTrue();
        _providerMock.Verify(p => p.GetByStateAsync("RS", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByStateAsync_SecondCall_UsesCacheInsteadOfProvider()
    {
        var municipalities = new List<Municipality> { new("Porto Alegre", "4314902") };

        _providerMock
            .Setup(p => p.GetByStateAsync("RS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(municipalities));

        await _sut.GetByStateAsync("RS", 1, 10, null);
        await _sut.GetByStateAsync("RS", 1, 10, null);

        _providerMock.Verify(p => p.GetByStateAsync("RS", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByStateAsync_PaginationCorrect_ReturnsSlicedData()
    {
        var municipalities = Enumerable.Range(1, 10)
            .Select(i => new Municipality($"Cidade {i}", $"430000{i}"))
            .ToList();

        _providerMock
            .Setup(p => p.GetByStateAsync("SP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(municipalities));

        var result = await _sut.GetByStateAsync("SP", 2, 3, null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(3);
        result.Value.Total.Should().Be(10);
        result.Value.TotalPages.Should().Be(4);
        result.Value.Page.Should().Be(2);
    }

    [Fact]
    public async Task GetByStateAsync_ProviderFails_ReturnsFailure()
    {
        _providerMock
            .Setup(p => p.GetByStateAsync("RS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Municipality>>.Failure("Serviço indisponível"));

        var result = await _sut.GetByStateAsync("RS", 1, 10, null);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e == "Serviço indisponível");
    }

    [Fact]
    public async Task GetByStateAsync_ResultsOrderedAlphabetically()
    {
        var municipalities = new List<Municipality>
        {
            new("Zacarias", "3556909"),
            new("Adamantina", "3500105"),
            new("Marília", "3529005")
        };

        _providerMock
            .Setup(p => p.GetByStateAsync("SP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(municipalities));

        var result = await _sut.GetByStateAsync("SP", 1, 10, null);

        var names = result.Value!.Data.Select(m => m.Name).ToList();
        names.Should().BeInAscendingOrder();
    }
}
