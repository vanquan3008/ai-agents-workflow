# syntax=docker/dockerfile:1.7
# One Dockerfile, two target images: `api` and `worker`. Build:
#   docker build --target api -t devagent-api ./backend
#   docker build --target worker -t devagent-worker ./backend

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore first so source-only changes reuse the cached layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/DevAgent.Domain/DevAgent.Domain.csproj src/DevAgent.Domain/
COPY src/DevAgent.Application/DevAgent.Application.csproj src/DevAgent.Application/
COPY src/DevAgent.Infrastructure/DevAgent.Infrastructure.csproj src/DevAgent.Infrastructure/
COPY src/DevAgent.Api/DevAgent.Api.csproj src/DevAgent.Api/
COPY src/DevAgent.Worker/DevAgent.Worker.csproj src/DevAgent.Worker/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/DevAgent.Api/DevAgent.Api.csproj && \
    dotnet restore src/DevAgent.Worker/DevAgent.Worker.csproj

COPY .editorconfig* ./
COPY src/ src/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/DevAgent.Api/DevAgent.Api.csproj -c Release -o /out/api --no-restore /p:UseAppHost=false && \
    dotnet publish src/DevAgent.Worker/DevAgent.Worker.csproj -c Release -o /out/worker --no-restore /p:UseAppHost=false

# ---------- API ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS api
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=0
COPY --from=build /out/api .
# Run as non-root (the `app` user ships with the .NET image).
USER app
EXPOSE 8080
# The aspnet image has no curl, so bash's /dev/tcp calls the readiness endpoint.
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/ready HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && grep -q "200 OK" <&3' || exit 1
ENTRYPOINT ["dotnet", "DevAgent.Api.dll"]

# ---------- Worker ----------
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION} AS worker
WORKDIR /app
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    Worker__HeartbeatFile=/tmp/devagent-worker.alive
COPY --from=build /out/worker .
USER app
# The worker is healthy when its heartbeat file was touched within the last 60 seconds.
HEALTHCHECK --interval=20s --timeout=5s --start-period=30s --retries=3 \
  CMD bash -c 'test -f /tmp/devagent-worker.alive && test $(( $(date +%s) - $(stat -c %Y /tmp/devagent-worker.alive) )) -lt 60' || exit 1
ENTRYPOINT ["dotnet", "DevAgent.Worker.dll"]
