FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["src/APITemplate.Api/APITemplate.Api.csproj", "APITemplate.Api/"]
COPY ["src/APITemplate.Application/APITemplate.Application.csproj", "APITemplate.Application/"]
COPY ["src/APITemplate.Domain/APITemplate.Domain.csproj", "APITemplate.Domain/"]
COPY ["src/APITemplate.Infrastructure/APITemplate.Infrastructure.csproj", "APITemplate.Infrastructure/"]
RUN dotnet restore "APITemplate.Api/APITemplate.Api.csproj"
COPY src/ ./
WORKDIR "/src/APITemplate.Api"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "APITemplate.Api.dll"]
