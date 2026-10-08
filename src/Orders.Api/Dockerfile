FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Build.props
COPY Directory.Packages.props Directory.Packages.props
COPY intralogistics-orchestrator.sln intralogistics-orchestrator.sln
COPY src/BuildingBlocks/BuildingBlocks.csproj src/BuildingBlocks/BuildingBlocks.csproj
COPY src/Orders.Domain/Orders.Domain.csproj src/Orders.Domain/Orders.Domain.csproj
COPY src/Orders.Application/Orders.Application.csproj src/Orders.Application/Orders.Application.csproj
COPY src/Orders.Infrastructure/Orders.Infrastructure.csproj src/Orders.Infrastructure/Orders.Infrastructure.csproj
COPY src/Orders.Api/Orders.Api.csproj src/Orders.Api/Orders.Api.csproj

RUN dotnet restore src/Orders.Api/Orders.Api.csproj

COPY src/ src/

RUN dotnet publish src/Orders.Api/Orders.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Orders.Api.dll"]
