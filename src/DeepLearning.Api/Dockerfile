# Build context is the repo root (not this directory) — the API project references sibling
# projects (Application/Domain/Infrastructure) that must be present in the build context too.
# Build with:  docker compose -f docker-compose.prod.yml build
# or directly: docker build -f src/DeepLearning.Api/Dockerfile -t deeplearning-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so dependency layers cache across code-only changes.
COPY DeepLearning.slnx ./
COPY src/DeepLearning.Api/DeepLearning.Api.csproj src/DeepLearning.Api/
COPY src/DeepLearning.Application/DeepLearning.Application.csproj src/DeepLearning.Application/
COPY src/DeepLearning.Domain/DeepLearning.Domain.csproj src/DeepLearning.Domain/
COPY src/DeepLearning.Infrastructure/DeepLearning.Infrastructure.csproj src/DeepLearning.Infrastructure/
RUN dotnet restore src/DeepLearning.Api/DeepLearning.Api.csproj

COPY src/DeepLearning.Api/ src/DeepLearning.Api/
COPY src/DeepLearning.Application/ src/DeepLearning.Application/
COPY src/DeepLearning.Domain/ src/DeepLearning.Domain/
COPY src/DeepLearning.Infrastructure/ src/DeepLearning.Infrastructure/

RUN dotnet publish src/DeepLearning.Api/DeepLearning.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Container-internal port only — never published straight to the host. In production the
# cloudflared sidecar (docker-compose.prod.yml) is the only thing that reaches this port, over
# the compose network, so the home machine's public IP/router are never exposed.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DeepLearning.Api.dll"]
