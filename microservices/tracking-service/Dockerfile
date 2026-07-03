FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["TrackingService.sln", "."]
COPY ["src/TrackingService.Api/TrackingService.Api.csproj", "src/TrackingService.Api/"]
COPY ["src/TrackingService.Application/TrackingService.Application.csproj", "src/TrackingService.Application/"]
COPY ["src/TrackingService.Domain/TrackingService.Domain.csproj", "src/TrackingService.Domain/"]
COPY ["src/TrackingService.Infrastructure/TrackingService.Infrastructure.csproj", "src/TrackingService.Infrastructure/"]
COPY ["tests/TrackingService.Tests/TrackingService.Tests.csproj", "tests/TrackingService.Tests/"]

RUN dotnet restore "TrackingService.sln"

COPY . .

RUN dotnet publish "src/TrackingService.Api/TrackingService.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "TrackingService.Api.dll"]
