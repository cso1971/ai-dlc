# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln .
COPY src/Shared/Contracts/*.csproj src/Shared/Contracts/
COPY src/Shared/Common/*.csproj src/Shared/Common/
COPY src/Services/Ordering.Api/*.csproj src/Services/Ordering.Api/
COPY src/Services/Invoicing.Api/*.csproj src/Services/Invoicing.Api/
COPY src/Services/Customers.Api/*.csproj src/Services/Customers.Api/

# Restore dependencies
RUN dotnet restore src/Services/Ordering.Api/Ordering.Api.csproj

# Copy source code
COPY src/Shared/ src/Shared/
COPY src/Services/Ordering.Api/ src/Services/Ordering.Api/

# Build and publish
RUN dotnet publish src/Services/Ordering.Api/Ordering.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Ordering.Api.dll"]
