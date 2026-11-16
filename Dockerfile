# KKBOT - Multi-stage Docker build
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln .
COPY src/**/*.csproj .
RUN for f in $(find . -name "*.csproj"); do mkdir -p $(dirname $f); done
COPY src/**/*.csproj ./src/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ ./src/
COPY tests/ ./tests/

# Build and publish
RUN dotnet publish src/KKBOT.UI.Dashboard/KKBOT.UI.Dashboard.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r kkbot && useradd -r -g kkbot kkbot
RUN mkdir -p /app/logs /app/plugins /app/data && chown -R kkbot:kkbot /app

# Copy published app
COPY --from=build /app/publish .

# Switch to non-root user
USER kkbot

# Expose ports
EXPOSE 8080 8081

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__KkbotDb="DataSource=/app/data/kkbot.db"
ENV Security__SecretsPath="/app/data/secrets.json"
ENV Plugins__Path="/app/plugins"

# Entry point
ENTRYPOINT ["dotnet", "KKBOT.UI.Dashboard.dll"]