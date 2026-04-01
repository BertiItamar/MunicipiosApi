# Municípios API

API REST em .NET 10 para consulta de municípios brasileiros por UF, com suporte a múltiplos providers de dados configuráveis via variável de ambiente.

## Tecnologias

- .NET 10 / ASP.NET Core
- Clean Architecture (Domain → Application → Infrastructure → Api)
- IMemoryCache (cache de 24h por UF)
- xUnit + Moq + FluentAssertions
- Swagger / OpenAPI
- Docker

## Como executar

### Local

```bash
dotnet run --project src/MunicipiosApi.Api
```

Acesse o Swagger em: `http://localhost:5000/swagger`

### Docker

```bash
docker-compose up -d
```

Acesse: `http://localhost:8080/swagger`

## Configuração do Provider

O provider de dados é definido via variável de ambiente `MUNICIPALITY_PROVIDER`.

| Valor | API utilizada |
|---|---|
| `BrasilApi` (padrão) | https://brasilapi.com.br |
| `Ibge` | https://servicodados.ibge.gov.br |

### Exemplos de configuração

```bash
# Local
MUNICIPALITY_PROVIDER=Ibge dotnet run --project src/MunicipiosApi.Api

# Docker
MUNICIPALITY_PROVIDER=Ibge docker-compose up -d
```

## Endpoints

### `GET /api/municipalities/{uf}`

Lista os municípios de um estado com paginação e busca.

**Parâmetros:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `uf` | string (path) | Sim | Sigla do estado (ex: RS, SP, MG) |
| `page` | int (query) | Não | Número da página (default: 1) |
| `pageSize` | int (query) | Não | Itens por página, máx 500 (default: 50) |
| `search` | string (query) | Não | Filtro por nome do município |

**Resposta 200:**

```json
{
  "data": [
    {
      "name": "Porto Alegre",
      "ibge_code": "4314902"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 497,
  "totalPages": 10
}
```

**Resposta 400:**

```json
{
  "errors": ["UF 'XX' inválida. Use a sigla de um dos 27 estados brasileiros."]
}
```

## Testes

```bash
# Todos os testes
dotnet test

# Unitários
dotnet test tests/MunicipiosApi.UnitTests

# Integração
dotnet test tests/MunicipiosApi.IntegrationTests
```

## Arquitetura

```
src/
├── MunicipiosApi.Domain          → Entidades, interfaces, Result<T>
├── MunicipiosApi.Application     → Services, DTOs, contratos
├── MunicipiosApi.Infrastructure  → Providers (BrasilAPI/IBGE), DI, cache
└── MunicipiosApi.Api             → Controllers, Middleware, configuração

tests/
├── MunicipiosApi.UnitTests       → Testes unitários do MunicipalityService (12 casos)
└── MunicipiosApi.IntegrationTests → Testes de integração via WebApplicationFactory (6 casos)
```

### Strategy Pattern para providers

O provider ativo é selecionado em tempo de startup via `DependencyInjection.cs`:

```csharp
if (provider == "Ibge")
    services.AddScoped<IMunicipalityProvider, IbgeMunicipalityProvider>();
else
    services.AddScoped<IMunicipalityProvider, BrasilApiMunicipalityProvider>();
```

Adicionar um novo provider requer apenas implementar `IMunicipalityProvider` e registrá-lo.

### Cache

Municípios são cacheados em memória por 24 horas por UF. A busca (`search`) e a paginação são aplicadas sobre os dados cacheados, evitando chamadas repetidas às APIs externas.
