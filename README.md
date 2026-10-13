# JNNchecking — License & Device Management Server

> **Máy chủ Quản lý Bản quyền & Xác thực Thiết bị cho Autodesk Revit Add-in [JNNTool](https://github.com/john-cpu25/JNNTool)**  
> Phát triển bằng **.NET 10 (ASP.NET Core)** theo kiến trúc **Clean Architecture**.

---

## 1. Tổng quan hệ thống (Architecture)

```
                            ┌─────────────────────────────────┐
                            │      JNNTool (Revit Add-in)     │
                            │      [Tools/Login Module]       │
                            └────────────────┬────────────────┘
                                             │ REST API (JSON / JWT)
                                             ▼
                      ┌──────────────────────────────────────────────┐
                      │    JNNchecking Server (ASP.NET Core)         │
                      ├──────────────────────┬───────────────────────┤
                      │ LicenseManagement.Api│LicenseManagement.Admin│
                      │  (Cổng REST API)     │  (Web Portal Quản trị)│
                      │  Port: 5000          │  Port: 5031           │
                      └──────────┬───────────┴───────────┬───────────┘
                                 │                       │
                                 ▼                       ▼
                      ┌──────────────────────────────────────────────┐
                      │    LicenseManagement.Application & Domain    │
                      └──────────────────────┬───────────────────────┘
                                             ▼
                      ┌──────────────────────────────────────────────┐
                      │         LicenseManagement.Infrastructure     │
                      │  (EF Core: SQLite / PostgreSQL, JWT, SHA256) │
                      └──────────────────────────────────────────────┘
```

### Các Project thành phần:
| Project | Mục đích | Công nghệ |
|---|---|---|
| **`LicenseManagement.Admin`** | Giao diện Web Portal quản trị cấp phép, duyệt đơn đăng ký, quản lý thiết bị, xem log hoạt động. | ASP.NET Core Razor Pages, Cookie Auth, CSS Responsive |
| **`LicenseManagement.Api`** | REST API hiệu năng cao cho Revit Add-in kết nối để kích hoạt, dùng thử và kiểm tra license định kỳ. | ASP.NET Core Web API, JWT Bearer, Rate-limiting |
| **`LicenseManagement.Application`** | Khai báo Interfaces, DTOs, Business Rules. | C# Class Library |
| **`LicenseManagement.Domain`** | Thực thể lõi (`License`, `User`, `Device`, `RegistrationRequest`, `AdminUser`, `AuditEvent`). | C# POCO Entities |
| **`LicenseManagement.Infrastructure`** | Triển khai DbContext (SQLite/PostgreSQL), băm mã kích hoạt SHA-256 + Pepper, sinh JWT Token. | EF Core 10, Npgsql, SQLite, System.Security |

---

## 2. Thông tin đăng nhập mặc định (Default Credentials)

Khi chạy lần đầu, hệ thống sẽ tự động khởi tạo cơ sở dữ liệu và tài khoản Quản trị tối cao (SuperAdmin):

| Mục | Giá trị |
|---|---|
| **Email đăng nhập** | `admin@jnntool.com` |
| **Mật khẩu** | `Admin@JNNTool2026!` |
| **Vai trò (Role)** | SuperAdmin |

> ⚠️ **Lưu ý bảo mật**: Sau khi đưa lên môi trường Production công khai, vui lòng đổi mật khẩu SuperAdmin hoặc đặt biến môi trường cấu hình bí mật.

---

## 3. Khởi chạy cục bộ (Local Development)

### Yêu cầu:
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (hoặc .NET 8 trở lên).

### Cách 1: Chạy bằng PowerShell Script (Khuyên dùng trên Windows)
Chỉ cần nhấp đúp hoặc chạy lệnh sau trong thư mục gốc:
```powershell
.\start_server.ps1
```
Script sẽ tự động khởi động:
- **Web Admin Dashboard**: `http://localhost:5031`
- **Revit REST API**: `http://localhost:5000`
- Tự mở trình duyệt trang Dashboard.

### Cách 2: Chạy thủ công từng project qua CLI
```bash
# Terminal 1: Chạy Web Admin Portal
dotnet run --project LicenseManagement.Admin --launch-profile http

# Terminal 2: Chạy Revit Client API
dotnet run --project LicenseManagement.Api --launch-profile http
```

---

## 4. Chạy với Docker & Docker Compose

Hệ thống đã có sẵn `docker-compose.yml` để chạy cả 2 dịch vụ độc lập với cơ chế tự khởi động lại và lưu trữ volume dữ liệu:

```bash
# Khởi động dịch vụ ở background
docker compose up -d

# Xem log hoạt động
docker compose logs -f

# Dừng hệ thống
docker compose down
```

- **Web Admin**: `http://<ip-cua-ban>:5031`
- **Revit API**: `http://<ip-cua-ban>:5000`
- Dữ liệu SQLite được lưu vào volume `jnnchecking-data:/data`.

---

## 5. Hướng dẫn Triển khai Cloud (Deploy Online lên Internet)

Để Revit Add-in của người dùng có thể kích hoạt từ mọi nơi qua Internet, bạn có thể triển khai `JNNchecking` lên các nền tảng Cloud miễn phí hoặc VPS:

### Cách 1: Triển khai lên Render.com (Miễn phí)
1. Đăng ký tài khoản tại [Render.com](https://render.com).
2. Chọn **New** → **Web Service** → Kết nối với repository GitHub: `https://github.com/john-cpu25/JNNchecking`.
3. Cấu hình triển khai:
   - **Name**: `jnnchecking-api` (hoặc `jnnchecking-admin`)
   - **Environment**: `Docker`
   - **DockerfilePath**: `Dockerfile.api` (cho cổng API) hoặc `Dockerfile.admin` (cho cổng Admin)
4. Thêm các biến môi trường (Environment Variables) trong phần **Settings**:
   - `ASPNETCORE_ENVIRONMENT`: `Production`
   - `Security__JwtSecret`: `Chuoi_Bi_Mat_Cua_Ban_Toi_Thieu_32_Ky_Tu!`
   - `Security__CodePepper`: `Chuoi_Pepper_Bi_Mat_Cua_Ban_2026!`
5. Nhấn **Deploy Web Service**. Render sẽ cung cấp một URL công khai dạng `https://jnnchecking-api.onrender.com`.

### Cách 2: Triển khai lên Railway.app
1. Truy cập [Railway.app](https://railway.app), tạo New Project từ GitHub Repo `JNNchecking`.
2. Railway tự động nhận diện Dockerfile hoặc .NET solution.
3. Thiết lập biến môi trường và Generate Domain công khai.

### Cách 3: Triển khai lên VPS Linux (Ubuntu / Debian)
1. Cài đặt .NET 10 runtime hoặc Docker trên VPS.
2. Clone repo về VPS:
   ```bash
   git clone https://github.com/john-cpu25/JNNchecking.git
   cd JNNchecking
   docker compose up -d
   ```
3. Cấu hình Nginx / Caddy Reverse Proxy kèm chứng chỉ SSL HTTPS miễn phí (Let's Encrypt).

---

## 6. Kết nối Revit Add-in (JNNTool) với Máy chủ

Sau khi máy chủ đã online (ví dụ tại `https://your-domain.com` hoặc `https://jnnchecking.onrender.com`):

### Cách 1: Thiết lập Biến môi trường trên máy tính chạy Revit
Đặt biến môi trường hệ thống:
```powershell
[System.Environment]::SetEnvironmentVariable('JNNTOOL_LICENSE_API_URL', 'https://your-api-domain.com', 'User')
```

### Cách 2: Cập nhật URL mặc định trong mã nguồn JNNTool
Trong `JNNTool/Tools/Login/Services/LicenseClientService.cs`:
```csharp
ServerBaseUrl = !string.IsNullOrWhiteSpace(envUrl) 
    ? envUrl.TrimEnd('/') 
    : "https://your-api-domain.com"; // Thay đổi URL máy chủ tại đây
```

---

## 7. Danh sách REST API Endpoints

| Method | Endpoint | Mô tả |
|---|---|---|
| `POST` | `/api/auth/register-request` | Gửi yêu cầu đăng ký thiết bị mới (Email, FullName, MachineId, DeviceName). |
| `POST` | `/api/auth/activate` | Kích hoạt bản quyền chính thức bằng mã Activation Code (GUID). |
| `POST` | `/api/auth/trial` | Kích hoạt bản quyền dùng thử 15 ngày (Trial). |
| `POST` | `/api/license/validate` | Xác thực định kỳ (Heartbeat), trả về trạng thái hiệu lực & thời hạn còn lại. |
| `POST` | `/api/license/deactivate` | Hủy kích hoạt trên thiết bị hiện tại để chuyển sang máy khác. |

---

## 8. Giấy phép & Bản quyền

Dự án thuộc quyền sở hữu của **JNNTool**.  
Mọi quyền được bảo lưu © 2026.
