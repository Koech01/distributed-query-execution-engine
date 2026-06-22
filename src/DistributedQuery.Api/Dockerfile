FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY DistributedQuery.sln ./
COPY src/DistributedQuery.Api/DistributedQuery.Api.csproj src/DistributedQuery.Api/
COPY src/DistributedQuery.Coordinator/DistributedQuery.Coordinator.csproj src/DistributedQuery.Coordinator/
COPY src/DistributedQuery.Core/DistributedQuery.Core.csproj src/DistributedQuery.Core/
COPY src/DistributedQuery.Infrastructure/DistributedQuery.Infrastructure.csproj src/DistributedQuery.Infrastructure/
COPY src/DistributedQuery.QueryParser/DistributedQuery.QueryParser.csproj src/DistributedQuery.QueryParser/
COPY src/DistributedQuery.Worker/DistributedQuery.Worker.csproj src/DistributedQuery.Worker/
RUN dotnet restore src/DistributedQuery.Api/DistributedQuery.Api.csproj

FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/DistributedQuery.Api/DistributedQuery.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "DistributedQuery.Api.dll"]
