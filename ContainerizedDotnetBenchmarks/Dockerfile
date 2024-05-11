FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ContainerizedDotnetBenchmarks/ContainerizedDotnetBenchmarks.csproj", "ContainerizedDotnetBenchmarks/"]
RUN dotnet restore "ContainerizedDotnetBenchmarks/ContainerizedDotnetBenchmarks.csproj"
COPY . .
WORKDIR "/src/ContainerizedDotnetBenchmarks"
RUN dotnet build "ContainerizedDotnetBenchmarks.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ContainerizedDotnetBenchmarks.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ContainerizedDotnetBenchmarks.dll"]
