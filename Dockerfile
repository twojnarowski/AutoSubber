# Use the official .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set the working directory
WORKDIR /src

# Copy solution file
COPY AutoSubber.sln ./

# Copy project files
COPY AutoSubber/AutoSubber/AutoSubber.csproj ./AutoSubber/AutoSubber/
COPY AutoSubber/AutoSubber.Client/AutoSubber.Client.csproj ./AutoSubber/AutoSubber.Client/
COPY AutoSubber.Tests/AutoSubber.Tests.csproj ./AutoSubber.Tests/

# Restore dependencies
RUN dotnet restore

# Copy the source code
COPY . .

# Build the application
RUN dotnet build --configuration Release --no-restore

# Publish the application
RUN dotnet publish AutoSubber/AutoSubber/AutoSubber.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish \
    --no-restore

# Use the official .NET 9.0 runtime image for the final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser

# Set the working directory
WORKDIR /app

# Create directories for data protection keys with proper permissions
RUN mkdir -p /var/keys && chown appuser:appuser /var/keys

# Copy the published application
COPY --from=build /app/publish .

# Change ownership of the app directory to the non-root user
RUN chown -R appuser:appuser /app

# Switch to the non-root user
USER appuser

# Expose the port the app runs on
EXPOSE 8080

# Configure the app to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080

# Configure production environment
ENV ASPNETCORE_ENVIRONMENT=Production

# Configure data protection key directory
ENV DataProtection__KeyDirectory=/var/keys

# Configure database to use SQLite by default in container
ENV DatabaseProvider=SQLite
ENV ConnectionStrings__SqliteConnection="Data Source=/app/autosubber.db"

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set the entry point
ENTRYPOINT ["dotnet", "AutoSubber.dll"]