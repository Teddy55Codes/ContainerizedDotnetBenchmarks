FROM mcr.microsoft.com/dotnet/sdk:8.0
USER root

ARG BUILD_CONFIGURATION=Release
ARG BenchmarkProjectPath

# copy benchmark project
COPY BenchmarkProj /BenchmarkProj
RUN dotnet restore /BenchmarkProj/$BenchmarkProjectPath


# copy projects
WORKDIR /src
COPY ContainerizedDotnetBenchmarks ContainerizedDotnetBenchmarks

# restore and build
RUN dotnet restore "ContainerizedDotnetBenchmarks/ContainerizedDotnetBenchmarks.csproj"
RUN dotnet publish "ContainerizedDotnetBenchmarks/ContainerizedDotnetBenchmarks.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# convert ARGs to ENVs to ensure they are available at runtime
ENV BenchmarkProjectPath=$BenchmarkProjectPath
ENV DotnetFramework=net8.0
ENV InstanceName=MyBenchmarkInstance
ENV ServerAddress=http://127.0.0.1:5000
ENV ServerPassword=password12345

# run
WORKDIR /app/publish
ENTRYPOINT dotnet ContainerizedDotnetBenchmarks.dll $BenchmarkProjectPath $DotnetFramework $InstanceName $ServerAddress $ServerPassword
