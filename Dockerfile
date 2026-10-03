# Multi-stage Container for .NET Enterprise Applications
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props* ./
COPY *.sln ./
COPY CsharpProjects/ ./CsharpProjects/

RUN dotnet restore || true
RUN dotnet build -c Release --no-restore || dotnet build -c Release

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /src ./
CMD ["dotnet", "--info"]
