# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["nuget.config", "."]
COPY ["Maliev.ProjectService.Api/Maliev.ProjectService.Api.csproj", "Maliev.ProjectService.Api/"]
COPY ["Maliev.ProjectService.Application/Maliev.ProjectService.Application.csproj", "Maliev.ProjectService.Application/"]
COPY ["Maliev.ProjectService.Domain/Maliev.ProjectService.Domain.csproj", "Maliev.ProjectService.Domain/"]
COPY ["Maliev.ProjectService.Infrastructure/Maliev.ProjectService.Infrastructure.csproj", "Maliev.ProjectService.Infrastructure/"]

RUN --mount=type=secret,id=nuget_username \
  --mount=type=secret,id=nuget_password \
  NUGET_USERNAME=$(cat /run/secrets/nuget_username) \
  NUGET_PASSWORD=$(cat /run/secrets/nuget_password) \
  dotnet restore "./Maliev.ProjectService.Api/Maliev.ProjectService.Api.csproj"

COPY . .
WORKDIR "/src/Maliev.ProjectService.Api"
RUN dotnet build "./Maliev.ProjectService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Maliev.ProjectService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN chown -R app:app /app
USER app
COPY --chown=app:app --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/project/liveness || exit 1

ENTRYPOINT ["dotnet", "Maliev.ProjectService.Api.dll"]
