# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Payment.sln", "./"]
COPY ["src/Payment.API/Payment.API.csproj", "src/Payment.API/"]
COPY ["src/Payment.Application/Payment.Application.csproj", "src/Payment.Application/"]
COPY ["src/Payment.Domain/Payment.Domain.csproj", "src/Payment.Domain/"]
COPY ["src/Payment.Infrastructure/Payment.Infrastructure.csproj", "src/Payment.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "Payment.sln"

# Copy all source files
COPY . .

# Build and publish
WORKDIR "/src/src/Payment.API"
RUN dotnet build "Payment.API.csproj" -c Release -o /app/build
RUN dotnet publish "Payment.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install EF Core tools for migrations
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Payment.API.dll"]

