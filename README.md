# Municípios API

API REST em .NET 10 para consulta de municípios brasileiros por UF.

Construída com Clean Architecture, cache distribuído via Redis e suporte a múltiplos providers de dados configuráveis por variável de ambiente.

---

## Tecnologias

| Camada | Tecnologia |
|---|---|
| Framework | .NET 10 / ASP.NET Core |
| Arquitetura | Clean Architecture (Domain → Application → Infrastructure → Api) |
| Cache | Redis (StackExchange.Redis) com fallback in-memory |
| Testes | xUnit + Moq + FluentAssertions |
| Documentação | Swagger / OpenAPI |
| CI | GitHub Actions |
| Container | Docker + Docker Compose |

---

## Como executar

### Local (sem Docker)

```bash
dotnet run --project src/MunicipiosApi.Api
```

O Swagger abre automaticamente em `http://localhost:5043/swagger`.

> Sem Redis configurado, a API usa cache in-memory automaticamente como fallback.

### Docker (API + Redis)

```bash
docker-compose up -d
```

Acesse: `http://localhost:8080/swagger`

---

## Configuração do Provider

O provider de dados é definido via variável de ambiente `MUNICIPALITY_PROVIDER`.

| Valor | API utilizada |
|---|---|
| `BrasilApi` (padrão) | https://brasilapi.com.br |
| `Ibge` | https://servicodados.ibge.gov.br |

Trocar o provider **não requer alteração de código** — apenas a variável de ambiente.

```bash
# Local
MUNICIPALITY_PROVIDER=Ibge dotnet run --project src/MunicipiosApi.Api

# Docker
MUNICIPALITY_PROVIDER=Ibge docker-compose up -d
```

---

## Configuração do Redis

A conexão com Redis é definida via `ConnectionStrings:Redis`.

```bash
# Docker Compose (já configurado)
ConnectionStrings__Redis=redis:6379

# Produção
ConnectionStrings__Redis=seu-redis-host:6379
```

> Quando a variável estiver vazia, a API usa cache in-memory como fallback sem nenhuma alteração de código.

---

## Endpoint

### `GET /api/municipalities/{uf}`

Lista os municípios de um estado com paginação e busca por nome.

**Parâmetros:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `uf` | string (path) | Sim | Sigla do estado (ex: RS, SP, MG) |
| `page` | int (query) | Não | Número da página — default: `1` |
| `pageSize` | int (query) | Não | Itens por página, máx 500 — default: `50` |
| `search` | string (query) | Não | Filtro parcial por nome do município |

**200 OK:**

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

**400 Bad Request:**

```json
{
  "errors": [
    { "description": "UF 'XX' inválida. Use a sigla de um dos 27 estados brasileiros." }
  ]
}
```

---

## Testes

```bash
# Todos os testes
dotnet test

# Unitários (12 casos)
dotnet test tests/MunicipiosApi.UnitTests

# Integração (6 casos)
dotnet test tests/MunicipiosApi.IntegrationTests
```

---

## Arquitetura

```
src/
├── MunicipiosApi.Domain          → Municipality, Result<T>, IMunicipalityProvider
├── MunicipiosApi.Application     → MunicipalityService, DTOs, paginação, cache, validação de UF
├── MunicipiosApi.Infrastructure  → BrasilApiProvider, IbgeProvider, DI, Redis
└── MunicipiosApi.Api             → MunicipalityController, ExceptionMiddleware,
                                    ResultExtensions, ErrorsViewModel, Swagger

tests/
├── MunicipiosApi.UnitTests       → MunicipalityService (12 testes com MemoryDistributedCache)
└── MunicipiosApi.IntegrationTests → Controller E2E via WebApplicationFactory (6 testes)
```

### Strategy Pattern — providers

O provider ativo é escolhido em tempo de startup via `DependencyInjection.cs`:

```csharp
if (provider == "Ibge")
    services.AddScoped<IMunicipalityProvider, IbgeMunicipalityProvider>();
else
    services.AddScoped<IMunicipalityProvider, BrasilApiMunicipalityProvider>();
```

Adicionar um novo provider requer apenas implementar `IMunicipalityProvider` e registrá-lo — sem alterar nenhuma outra classe.

### Result\<T\> + ToActionResult

Todo método de serviço retorna `Result<T>`. O controller nunca decide o status HTTP diretamente:

```csharp
// Controller
var result = await service.GetByStateAsync(uf, page, pageSize, search, ct);
return result.ToActionResult(); // → 200 OK ou 400 Bad Request automaticamente
```

### Cache distribuído

Municípios são cacheados no Redis por 24 horas por UF. Busca e paginação são aplicadas sobre os dados em cache, evitando chamadas repetidas às APIs externas.

```
1ª requisição → Redis (miss) → BrasilAPI/IBGE → salva no Redis → resposta
2ª requisição → Redis (hit)  → resposta  (API externa não é chamada)
```

Se o Redis estiver indisponível, a API continua funcionando e loga um warning.
