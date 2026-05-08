FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Kahoot.Backend.Domain/Kahoot.Backend.Domain.csproj", "src/Kahoot.Backend.Domain/"]
COPY ["src/Kahoot.Backend.Application/Kahoot.Backend.Application.csproj", "src/Kahoot.Backend.Application/"]
COPY ["src/Kahoot.Backend.Infrastructure/Kahoot.Backend.Infrastructure.csproj", "src/Kahoot.Backend.Infrastructure/"]
COPY ["src/Kahoot.Backend.Api/Kahoot.Backend.Api.csproj", "src/Kahoot.Backend.Api/"]
RUN dotnet restore "src/Kahoot.Backend.Api/Kahoot.Backend.Api.csproj"

COPY . .
WORKDIR "/src/src/Kahoot.Backend.Api"
RUN dotnet build "Kahoot.Backend.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Kahoot.Backend.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kahoot.Backend.Api.dll"]
