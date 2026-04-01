FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/MunicipiosApi.Api/MunicipiosApi.Api.csproj", "src/MunicipiosApi.Api/"]
COPY ["src/MunicipiosApi.Application/MunicipiosApi.Application.csproj", "src/MunicipiosApi.Application/"]
COPY ["src/MunicipiosApi.Infrastructure/MunicipiosApi.Infrastructure.csproj", "src/MunicipiosApi.Infrastructure/"]
COPY ["src/MunicipiosApi.Domain/MunicipiosApi.Domain.csproj", "src/MunicipiosApi.Domain/"]
RUN dotnet restore "src/MunicipiosApi.Api/MunicipiosApi.Api.csproj"

COPY . .
WORKDIR "/src/src/MunicipiosApi.Api"
RUN dotnet build "MunicipiosApi.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MunicipiosApi.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MunicipiosApi.Api.dll"]
