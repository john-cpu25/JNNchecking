# ─── Multi-Stage Dockerfile for JNNchecking All-in-One Server ─────────
# Runs both Web Admin Dashboard (/) and Revit REST API (/api/...) on a single port!
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all project csproj files for layer caching
COPY ["LicenseManagement.Domain/LicenseManagement.Domain.csproj", "LicenseManagement.Domain/"]
COPY ["LicenseManagement.Application/LicenseManagement.Application.csproj", "LicenseManagement.Application/"]
COPY ["LicenseManagement.Infrastructure/LicenseManagement.Infrastructure.csproj", "LicenseManagement.Infrastructure/"]
COPY ["LicenseManagement.Api/LicenseManagement.Api.csproj", "LicenseManagement.Api/"]
COPY ["LicenseManagement.Admin/LicenseManagement.Admin.csproj", "LicenseManagement.Admin/"]

RUN dotnet restore "LicenseManagement.Admin/LicenseManagement.Admin.csproj"

# Copy remaining source code and publish
COPY . .
WORKDIR "/src/LicenseManagement.Admin"
RUN dotnet publish "LicenseManagement.Admin.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ─── Runtime Image ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# SQLite Persistent Data Directory
VOLUME ["/data"]
ENV ConnectionStrings__DefaultConnection="Data Source=/data/licenses.db"

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "LicenseManagement.Admin.dll"]
