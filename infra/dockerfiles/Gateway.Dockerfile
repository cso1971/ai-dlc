# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/Services/Gateway/*.csproj src/Services/Gateway/

RUN dotnet restore src/Services/Gateway/Gateway.csproj

COPY src/Services/Gateway/ src/Services/Gateway/

RUN dotnet publish src/Services/Gateway/Gateway.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Gateway.dll"]
