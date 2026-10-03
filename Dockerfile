# Multi-stage Container for .NET 9.0 Enterprise Applications
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props* ./
COPY *.sln ./
COPY CsharpProjects/ ./CsharpProjects/
COPY tests/ ./tests/

RUN dotnet restore
RUN dotnet build -c Release --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /src ./
CMD ["dotnet", "--info"]
