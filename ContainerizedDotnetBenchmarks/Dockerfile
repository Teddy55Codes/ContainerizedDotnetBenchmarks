FROM mcr.microsoft.com/dotnet/sdk:8.0
USER root

ARG BUILD_CONFIGURATION=Release
ARG BenchmarkProjectsParentDirectoryPath
ARG BenchmarkProjectProjectFilePaths
ARG TFMsForBenchmarkProjects

# copy benchmark project
COPY $BenchmarkProjectsParentDirectoryPath /BenchmarkProj

# copy projects
WORKDIR /src
COPY ContainerizedDotnetBenchmarks ContainerizedDotnetBenchmarks

# restore and build
RUN dotnet restore "ContainerizedDotnetBenchmarks/ContainerizedDotnetBenchmarks.csproj"
RUN dotnet publish "ContainerizedDotnetBenchmarks/ContainerizedDotnetBenchmarks.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# convert ARGs to ENVs to ensure they are available at runtime
ENV BenchmarkProjectProjectFilePaths=$BenchmarkProjectProjectFilePaths
ENV TFMsForBenchmarkProjects=$TFMsForBenchmarkProjects
ENV InstanceName=MyBenchmarkInstance
ENV ServerAddress=http://127.0.0.1:5000
ENV ServerPassword=password12345

# run
WORKDIR /BenchmarkProj
ENTRYPOINT dotnet /app/publish/ContainerizedDotnetBenchmarks.dll $BenchmarkProjectProjectFilePaths $TFMsForBenchmarkProjects $InstanceName $ServerAddress $ServerPassword
