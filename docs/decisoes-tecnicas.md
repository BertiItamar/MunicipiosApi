# Documento de Decisões Técnicas — Municípios API

> Documento de estudo descrevendo todas as decisões de arquitetura, tecnologias escolhidas e o raciocínio por trás de cada uma delas.

---

## 1. Visão Geral do Projeto

A Municípios API é uma API REST desenvolvida em .NET 10 que permite consultar municípios brasileiros por UF (Unidade Federativa). O projeto foi construído como um desafio técnico, priorizando:

- Clareza de código e separação de responsabilidades
- Extensibilidade sem modificação de código existente
- Testabilidade em todas as camadas
- Boas práticas de mercado

---

## 2. Arquitetura — Clean Architecture

### O que é

Clean Architecture é um modelo arquitetural criado por Robert C. Martin (Uncle Bob) que organiza o código em camadas concêntricas, onde as camadas internas não conhecem as externas.

```
        [ Domain ]
            ↑
       [ Application ]
            ↑
     [ Infrastructure ]
            ↑
          [ Api ]
```

### Como aplicamos

| Camada | Projeto | Responsabilidade |
|---|---|---|
| **Domain** | `MunicipiosApi.Domain` | Entidades de negócio, interfaces, Result\<T\> — zero dependências externas |
| **Application** | `MunicipiosApi.Application` | Regras de negócio, serviços, DTOs, validações |
| **Infrastructure** | `MunicipiosApi.Infrastructure` | Implementações técnicas: providers HTTP, cache Redis, DI |
| **Api** | `MunicipiosApi.Api` | Controllers, middlewares, configuração do ASP.NET Core |

### Por que usamos

- **Independência de frameworks**: o domínio não depende de ASP.NET, Redis, ou qualquer biblioteca externa. Se trocarmos o framework amanhã, o negócio não muda.
- **Testabilidade**: como as regras de negócio estão em `Application`, é trivial testá-las com mocks sem subir servidor ou banco.
- **Direção de dependência**: a regra de ouro é que dependências só apontam para dentro (Domain). Nunca o contrário.

### Exemplo prático

O `MunicipalityService` conhece `IMunicipalityProvider` (interface do Domain), mas **não sabe** se a implementação usa BrasilAPI, IBGE ou qualquer outra fonte. Isso é inversão de dependência (o "D" do SOLID).

---

## 3. .NET 10 e ASP.NET Core

### Por que .NET 10

- Versão mais recente e performática da plataforma
- Suporte nativo a primary constructors, collections expressions e outros recursos modernos do C#
- `IDistributedCache`, `IMemoryCache`, `IHttpClientFactory` são abstrações nativas — sem bibliotecas extras para padrões comuns

### Por que ASP.NET Core com Controllers

O template padrão do .NET oferece também Minimal APIs (sem controllers). Optamos por Controllers porque:

- **Organização**: cada controller agrupa endpoints de um recurso
- **Atributos**: `[ProducesResponseType]`, `[Route]`, `[ApiController]` enriquecem o Swagger automaticamente
- **Familiaridade**: padrão amplamente adotado em times e mais fácil de avaliar em desafios técnicos

---

## 4. Strategy Pattern — Troca de Provider sem alterar código

### O problema

O desafio exige suportar dois providers de dados (BrasilAPI e IBGE) e alternar entre eles via variável de ambiente. A solução ingênua seria um `if` no serviço:

```csharp
// ❌ Ruim — viola Open/Closed Principle
if (provider == "BrasilApi")
    // chama BrasilAPI
else
    // chama IBGE
```

### A solução — Strategy Pattern

Criamos uma interface no Domain que define o contrato:

```csharp
// Domain/Interfaces/IMunicipalityProvider.cs
public interface IMunicipalityProvider
{
    string ProviderName { get; }
    Task<Result<IEnumerable<Municipality>>> GetByStateAsync(string uf, CancellationToken ct);
}
```

Duas implementações concretas na Infrastructure:

```csharp
// Infrastructure/Providers/BrasilApiMunicipalityProvider.cs
public class BrasilApiMunicipalityProvider : IMunicipalityProvider { ... }

// Infrastructure/Providers/IbgeMunicipalityProvider.cs
public class IbgeMunicipalityProvider : IMunicipalityProvider { ... }
```

A escolha acontece apenas na configuração de DI, no startup:

```csharp
// Infrastructure/DependencyInjection.cs
if (provider == "Ibge")
    services.AddScoped<IMunicipalityProvider, IbgeMunicipalityProvider>();
else
    services.AddScoped<IMunicipalityProvider, BrasilApiMunicipalityProvider>();
```

### Por que isso importa

- O `MunicipalityService` chama `_provider.GetByStateAsync()` sem saber qual implementação está ativa
- Para adicionar um terceiro provider (ex: SIDRA/IBGE v2), basta criar uma nova classe e registrá-la no DI — **nenhuma outra classe é modificada**
- Isso é o princípio Open/Closed: aberto para extensão, fechado para modificação

---

## 5. Result\<T\> — Tratamento de erros sem exceções

### O problema

O comportamento padrão do C# é usar `throw Exception` para sinalizar erros. Isso tem problemas:

- Exceções são caras de processar (stack trace, heap allocation)
- O compilador não te obriga a tratar o erro — você pode esquecer o `try/catch`
- Mistura fluxo de negócio (UF inválida) com erros técnicos (banco caiu)

### A solução — Result\<T\>

```csharp
// Domain/Models/Result.cs
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new([error]);
}
```

Todo método de serviço retorna `Result<T>`. O chamador é forçado a verificar o resultado:

```csharp
// Application/Services/MunicipalityService.cs
public async Task<Result<PagedResultDto<MunicipalityDto>>> GetByStateAsync(...)
{
    if (!ValidUfs.Contains(normalizedUf))
        return Result<PagedResultDto<MunicipalityDto>>.Failure("UF inválida.");

    // ... lógica
    return Result<PagedResultDto<MunicipalityDto>>.Success(pagedResult);
}
```

### Por que isso importa

- Erros de negócio (validação) e erros técnicos (exceções) ficam claramente separados
- O controller nunca precisa de `try/catch` para erros de negócio
- Múltiplos erros podem ser retornados de uma vez (`Failure(IReadOnlyList<string>)`)

---

## 6. ToActionResult — Conversão automática de Result para HTTP

### O problema

Sem essa extension, cada controller precisaria decidir qual status HTTP retornar:

```csharp
// ❌ Ruim — lógica HTTP espalhada por todos os controllers
if (result.IsFailure)
    return BadRequest(new { errors = result.Errors });
return Ok(result.Value);
```

### A solução

```csharp
// Api/Extensions/ResultExtensions.cs
public static IActionResult ToActionResult<T>(this Result<T> result)
{
    if (result.IsFailure)
        return new BadRequestObjectResult(new ErrorsViewModel(
            result.Errors.Select(e => new ErrorViewModel(e)).ToList()));

    return new OkObjectResult(result.Value);
}
```

O controller fica com uma única responsabilidade — orquestrar:

```csharp
// Api/Controllers/MunicipalityController.cs
public async Task<IActionResult> GetByStateAsync(...)
{
    var result = await service.GetByStateAsync(uf, page, pageSize, search, ct);
    return result.ToActionResult(); // ← uma linha, sem if/else
}
```

### Por que isso importa

- Se precisarmos mudar o formato de erro, alteramos **um arquivo** (`ResultExtensions.cs`), não todos os controllers
- O controller não tem `if` — é puramente uma entrada e saída
- Formato de resposta sempre consistente em toda a API

---

## 7. ErrorsViewModel — Formato padronizado de erros

### Estrutura

```csharp
public record ErrorViewModel(string Description);
public record ErrorsViewModel(IReadOnlyList<ErrorViewModel> Errors);
```

### Resposta de erro sempre neste formato

```json
{
  "errors": [
    { "description": "UF 'XX' inválida." }
  ]
}
```

### Por que usamos record ao invés de class

`record` em C# é imutável por padrão, tem `Equals` e `ToString` gerados automaticamente e sintaxe mais concisa. Para ViewModels de resposta (que nunca são modificados após criados), é a escolha ideal.

---

## 8. Cache Distribuído com Redis

### Por que cache

As APIs externas (BrasilAPI e IBGE) retornam dados que raramente mudam — a lista de municípios de um estado é praticamente estática. Sem cache, cada requisição faria uma chamada HTTP externa, gerando:

- Latência desnecessária (~200-500ms por chamada)
- Dependência de disponibilidade de terceiros
- Risco de rate limiting

### Por que Redis e não IMemoryCache

| Critério | IMemoryCache | Redis |
|---|---|---|
| Escala horizontal | ❌ Cache por instância | ✅ Compartilhado entre todas as instâncias |
| Persistência | ❌ Perde ao reiniciar | ✅ Configurável |
| Tamanho | Limitado pela RAM da aplicação | Servidor dedicado |
| Observabilidade | Nenhuma | Redis CLI, RedisInsight |

Em produção com múltiplas instâncias da API, `IMemoryCache` causaria cache miss em toda nova instância. Redis resolve isso com um cache centralizado.

### Como implementamos

Usamos `IDistributedCache` — a **abstração** do .NET, não a implementação direta do Redis:

```csharp
// Application/Services/MunicipalityService.cs
private readonly IDistributedCache _cache; // ← interface, não StackExchange.Redis
```

Isso significa que nos testes usamos `MemoryDistributedCache` (in-memory) sem nenhuma mudança no código da regra de negócio.

### Fallback automático

```csharp
// Infrastructure/DependencyInjection.cs
if (!string.IsNullOrWhiteSpace(redisConnection))
    services.AddStackExchangeRedisCache(...); // Redis real
else
    services.AddDistributedMemoryCache();     // Fallback in-memory
```

Se o Redis não estiver configurado (desenvolvimento local sem Docker), a API usa memória sem nenhuma alteração de código.

### Resiliência

Se o Redis cair em produção, não derrubamos a API:

```csharp
try
{
    var cached = await cache.GetStringAsync(cacheKey, ct);
    // usa cache
}
catch (Exception ex)
{
    // Redis indisponível — loga warning e consulta o provider diretamente
    logger.LogWarning(ex, "Cache indisponível para UF {Uf}", uf);
}
```

### TTL — Time To Live

Municípios ficam 24 horas em cache. Esse valor foi escolhido porque:
- Municípios brasileiros raramente mudam (criação/extinção é evento raro)
- 24h garante que dados sazonais (se houvessem) seriam atualizados diariamente
- Reduz chamadas às APIs externas em ~99% após a primeira requisição por UF

---

## 9. IHttpClientFactory — Clientes HTTP gerenciados

### Por que não `new HttpClient()`

Criar `new HttpClient()` a cada requisição é um erro clássico em .NET:
- Exaure portas TCP disponíveis (socket exhaustion)
- Não respeita DNS TTL (problemas com Kubernetes/service discovery)

### A solução

```csharp
// Infrastructure/DependencyInjection.cs
services.AddHttpClient("BrasilApi", client =>
{
    client.BaseAddress = new Uri("https://brasilapi.com.br/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

`IHttpClientFactory` gerencia o pool de conexões e o ciclo de vida dos handlers. Os providers recebem a factory por injeção:

```csharp
public class BrasilApiMunicipalityProvider(IHttpClientFactory httpClientFactory, ...)
{
    var client = httpClientFactory.CreateClient("BrasilApi");
}
```

---

## 10. ExceptionMiddleware — Tratamento global de exceções

### Por que middleware e não try/catch em cada controller

Um middleware intercepta **toda** requisição antes de chegar ao controller. Isso garante que qualquer exceção não tratada (bug, falha de infraestrutura) seja capturada em um único lugar.

```csharp
// Api/Middleware/ExceptionMiddleware.cs
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await next(context); // executa toda a pipeline
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        context.Response.StatusCode = 499; // cliente cancelou a requisição
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Exceção não tratada");
        await WriteErrorResponseAsync(context, ex);
    }
}
```

### Status 499

O código 499 não é oficial do HTTP (é um código do Nginx), mas é amplamente adotado para indicar que o **cliente** cancelou a requisição (ex: usuário fechou o browser). Tratamos separado para não logar como erro de servidor.

### Mensagens por ambiente

```csharp
var message = env.IsDevelopment()
    ? ex.Message         // stack trace completo em desenvolvimento
    : "Ocorreu um erro interno."; // mensagem genérica em produção (segurança)
```

Não expor detalhes de exceção em produção é uma prática de segurança — evita vazar informações sobre a infraestrutura interna.

---

## 11. Paginação

### Por que paginar

A API do IBGE retorna até ~5570 municípios (Brasil inteiro). Retornar tudo de uma vez seria:
- Payload grande (~500KB de JSON)
- Lento para o cliente processar
- Ineficiente em mobile

### Como implementamos

```csharp
// Aplicada APÓS o cache e o filtro de busca
var total = dtos.Count;
var totalPages = (int)Math.Ceiling(total / (double)pageSize);
var paged = dtos.Skip((page - 1) * pageSize).Take(pageSize);
```

A paginação acontece em memória sobre os dados já cacheados — não fazemos paginação na API externa.

### Resposta

```json
{
  "data": [...],
  "page": 1,
  "pageSize": 50,
  "total": 497,
  "totalPages": 10
}
```

O cliente sabe exatamente quantas páginas existem e pode navegar sem fazer chamadas extras.

---

## 12. Testes

### Estratégia de testes

Usamos dois níveis de teste:

| Tipo | Projeto | Ferramenta | O que testa |
|---|---|---|---|
| Unitário | `MunicipiosApi.UnitTests` | xUnit + Moq + FluentAssertions | `MunicipalityService` isolado |
| Integração | `MunicipiosApi.IntegrationTests` | xUnit + WebApplicationFactory | Controller + pipeline completa |

### Testes unitários

Testam o `MunicipalityService` com o provider mockado (Moq) e cache in-memory real (`MemoryDistributedCache`):

```csharp
// Arrange
_providerMock
    .Setup(p => p.GetByStateAsync("RS", It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<IEnumerable<Municipality>>.Success(municipalities));

// Act
var result = await _sut.GetByStateAsync("RS", 1, 10, null);

// Assert
result.IsSuccess.Should().BeTrue();
result.Value!.Data.Should().HaveCount(3);
```

**Por que mockar o provider e não o cache?**
O cache é parte da lógica de negócio do serviço (verificar se chama o provider uma ou duas vezes). O provider é uma dependência externa que não queremos chamar nos testes unitários.

### Testes de integração

Usam `WebApplicationFactory<Program>` para subir a API completa em memória e substituir o provider por um mock:

```csharp
_factory.WithWebHostBuilder(builder =>
{
    builder.ConfigureServices(services =>
    {
        // Remove o provider real e injeta o mock
        services.Remove(services.Single(d => d.ServiceType == typeof(IMunicipalityProvider)));
        services.AddScoped<IMunicipalityProvider>(_ => providerMock.Object);
    });
}).CreateClient();
```

Isso testa a pipeline completa: roteamento, middleware, serialização JSON, status codes — sem chamar APIs externas reais.

### FluentAssertions

```csharp
// Sem FluentAssertions
Assert.Equal(2, result.Value.Data.Count());

// Com FluentAssertions — leitura natural
result.Value!.Data.Should().HaveCount(2);
result.Value.Data.Should().AllSatisfy(m => m.Name.Should().Contain("Porto"));
```

Além de mais legível, o FluentAssertions gera mensagens de erro muito mais detalhadas quando um teste falha.

---

## 13. Docker e Docker Compose

### Dockerfile — Multi-stage build

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build   # imagem com SDK para compilar
# ... compila e publica

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final # imagem só com runtime (menor)
COPY --from=build /app/publish .
```

Multi-stage build resulta em imagem final menor (~200MB vs ~800MB com SDK completo) porque o SDK não vai para produção.

### Docker Compose

```yaml
services:
  api:
    depends_on:
      redis:
        condition: service_healthy  # API só sobe quando Redis estiver pronto

  redis:
    image: redis:7-alpine           # alpine = imagem minimalista (~30MB)
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
```

O `depends_on` com `condition: service_healthy` garante que a API não sobe antes do Redis estar respondendo — evita erros de conexão no startup.

---

## 14. GitHub Actions — CI/CD

### O que é CI

Integração Contínua (CI) é a prática de validar automaticamente cada mudança de código. Toda vez que há um push, o GitHub sobe uma máquina virtual, compila o projeto e roda os testes.

### Nossa pipeline

```yaml
on:
  push:
    branches: ["main", "master"]
  pull_request:
    branches: ["main", "master"]

steps:
  - dotnet restore   # baixa dependências
  - dotnet build     # compila
  - dotnet test      # roda os 18 testes
```

### Por que isso importa

- **Garante que o código na `main` sempre compila e os testes passam**
- Em times, nenhum PR é mergeado com testes quebrando
- O avaliador do desafio vê o badge verde ✅ e sabe que o projeto está saudável

---

## 15. Commits Atômicos

### O que é

Um commit atômico contém **uma única responsabilidade lógica** — uma feature, uma correção, uma refatoração.

### Como estruturamos

```
chore: initialize solution with Clean Architecture structure
feat(domain): add Municipality model, Result<T> pattern and IMunicipalityProvider
feat(application): add MunicipalityService with caching, pagination and UF validation
feat(infrastructure): add BrasilAPI and IBGE providers with strategy pattern via env var
feat(api): add MunicipalityController, ExceptionMiddleware, Swagger and CORS
test: add 12 unit tests and 6 integration tests with Moq and FluentAssertions
chore: add Docker, GitHub Actions CI workflow and README
refactor(api): align response pattern with CRM Juridico standards
feat(cache): replace IMemoryCache with IDistributedCache (Redis)
docs: update README with Redis, architecture and pattern documentation
```

### Prefixos (Conventional Commits)

| Prefixo | Uso |
|---|---|
| `feat` | Nova funcionalidade |
| `fix` | Correção de bug |
| `refactor` | Refatoração sem mudança de comportamento |
| `test` | Adição ou alteração de testes |
| `chore` | Tarefas de manutenção (CI, Docker, configuração) |
| `docs` | Documentação |

Esse padrão permite gerar changelogs automaticamente e facilita o code review — o avaliador sabe o que cada commit faz só pelo título.

---

## 16. Princípios SOLID aplicados

| Princípio | Onde aplicamos |
|---|---|
| **S** — Single Responsibility | Cada classe tem uma responsabilidade: Service valida e orquestra, Provider busca dados, Middleware trata exceções |
| **O** — Open/Closed | Novo provider = nova classe, sem modificar `MunicipalityService` |
| **L** — Liskov Substitution | `BrasilApiProvider` e `IbgeProvider` são intercambiáveis via `IMunicipalityProvider` |
| **I** — Interface Segregation | `IMunicipalityProvider` tem apenas o contrato necessário |
| **D** — Dependency Inversion | `MunicipalityService` depende de `IMunicipalityProvider` (abstração), não da implementação concreta |

---

## Resumo das Decisões

| Decisão | Alternativa rejeitada | Motivo da escolha |
|---|---|---|
| Clean Architecture | MVC simples | Testabilidade e separação de responsabilidades |
| Strategy Pattern para providers | `if/else` no serviço | Open/Closed Principle — extensível sem modificar código |
| Result\<T\> | throw Exception | Fluxo de negócio explícito, sem exceções para validações |
| ToActionResult extension | if/else em cada controller | Formato de resposta centralizado e consistente |
| IDistributedCache (Redis) | IMemoryCache | Escala horizontal, compartilhado entre instâncias |
| Fallback in-memory | Obrigar Redis | Desenvolvimento local sem dependências externas |
| MemoryDistributedCache nos testes | Redis real nos testes | Testes rápidos e sem infraestrutura externa |
| WebApplicationFactory | Testes unitários dos controllers | Testa a pipeline HTTP completa (roteamento, serialização) |
| Multi-stage Dockerfile | Imagem única com SDK | Imagem de produção menor e mais segura |
| Conventional Commits | Commits sem padrão | Legibilidade do histórico e changelogs automáticos |
