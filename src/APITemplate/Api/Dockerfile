# Build context: repository root (docker-compose.yml sets context: .)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy root configuration files first for better caching and TargetFramework resolution
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]

# Copy all source code (contents of src/ into /src/)
COPY ["src/", "./"]

# Restore targeting the main project
RUN dotnet restore "APITemplate/Api/APITemplate.csproj"

# Build
WORKDIR "/src/APITemplate/Api"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "APITemplate.dll"]
