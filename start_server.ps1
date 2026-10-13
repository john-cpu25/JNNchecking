# JNNchecking License Server Launcher (Local Development)
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      JNNCHECKING LICENSE SYSTEM (ADMIN PORTAL & API)     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$adminProject = "LicenseManagement.Admin/LicenseManagement.Admin.csproj"
$apiProject = "LicenseManagement.Api/LicenseManagement.Api.csproj"

Write-Host "1. Starting Web Admin Portal on http://localhost:5031 ..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "run --project `"$adminProject`" --launch-profile http"

Start-Sleep -Seconds 2

Write-Host "2. Starting Revit License API on http://localhost:5000 ..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "run --project `"$apiProject`" --launch-profile http"

Start-Sleep -Seconds 2

Write-Host "3. Opening Web Admin Portal in default browser..." -ForegroundColor Yellow
Start-Process "http://localhost:5031"

Write-Host "`nAll services started successfully!" -ForegroundColor Green
Write-Host "  - Web Admin Dashboard: http://localhost:5031" -ForegroundColor White
Write-Host "  - Revit Client API:    http://localhost:5000" -ForegroundColor White
Write-Host "  - Default SuperAdmin:  admin@jnntool.com / Admin@JNNTool2026!" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
