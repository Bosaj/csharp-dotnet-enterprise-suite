# Multi-stage Hardened Container for .NET 9.0 Enterprise Applications
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props* ./
COPY *.sln ./
COPY CsharpProjects/ ./CsharpProjects/
COPY tests/ ./tests/
COPY Protos/ ./Protos/

RUN dotnet restore EniadCsharpDotnet.sln
RUN dotnet build EniadCsharpDotnet.sln -c Release --no-restore -p:CI=true
RUN dotnet test EniadCsharpDotnet.sln -c Release --no-build --no-restore
RUN dotnet publish CsharpProjects/EniadCsharpDotnet/EniadCsharpDotnet.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
RUN adduser -D -u 10001 appuser && chown -R appuser:appuser /app
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD dotnet EniadCsharpDotnet.dll 500 600 | grep -q 600 || exit 1

ENTRYPOINT ["dotnet", "EniadCsharpDotnet.dll"]
