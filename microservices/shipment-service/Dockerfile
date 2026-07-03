FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["ShipmentService.sln", "."]
COPY ["src/ShipmentService.Api/ShipmentService.Api.csproj", "src/ShipmentService.Api/"]
COPY ["src/ShipmentService.Application/ShipmentService.Application.csproj", "src/ShipmentService.Application/"]
COPY ["src/ShipmentService.Domain/ShipmentService.Domain.csproj", "src/ShipmentService.Domain/"]
COPY ["src/ShipmentService.Infrastructure/ShipmentService.Infrastructure.csproj", "src/ShipmentService.Infrastructure/"]
COPY ["tests/ShipmentService.Tests/ShipmentService.Tests.csproj", "tests/ShipmentService.Tests/"]

RUN dotnet restore "ShipmentService.sln"

COPY . .

RUN dotnet publish "src/ShipmentService.Api/ShipmentService.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "ShipmentService.Api.dll"]
