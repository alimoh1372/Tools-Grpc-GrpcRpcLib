# GrpcRpcLib Project

[English](#english) | [فارسی](#فارسی)

---

## English

### Overview
GrpcRpcLib is a .NET 8-based project implementing a gRPC-based messaging system with a Publisher-Consumer architecture. It supports multiple database backends (Sqlite, SqlServer, InMemory) for message storage and provides a robust way to send and process messages using gRPC.

This README provides detailed instructions on how to set up, configure, and run the project, including details on configuring `appsettings.json`, database options, and gRPC settings.

### Table of Contents
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Setup Instructions](#setup-instructions)
- [Configuration](#configuration)
  - [Consumer Configuration](#consumer-configuration)
  - [Publisher Configuration](#publisher-configuration)
  - [Message Store Configuration](#message-store-configuration)
- [Running the Project](#running-the-project)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

### Project Structure
The solution consists of the following projects:

- **GrpcRpcLib.Shared**: Contains shared logic, including `MessageDbContext`, `IMessageStore`, and database-related configurations (Sqlite, SqlServer, InMemory).
- **GrpcRpcLib.Consumer**: Implements the gRPC Consumer service that receives and processes messages.
- **GrpcRpcLib.Publisher**: Implements the Publisher service that sends messages to the Consumer via gRPC.
- **GrpcRpcLib.Test.Publisher**: A test project to simulate sending messages from the Publisher.

### Prerequisites
Ensure you have the following installed:

- **.NET 8 SDK**: Download from [Microsoft .NET](https://dotnet.microsoft.com/download).
- **Visual Studio 2022 (or later)** or any IDE supporting .NET 8 (e.g., Rider, VS Code).
- **SQLite (optional, for Sqlite database)**: Install sqlite3 or a SQLite browser (e.g., DB Browser for SQLite).
- **gRPC Tools**: For testing gRPC services, install grpcurl:
  ```bash
  go install github.com/fullstorydev/grpcurl/...@latest
  ```

#### Required NuGet Packages
Add the following NuGet packages to the respective projects:
```bash
dotnet add package Grpc.AspNetCore -Version 2.57.0
dotnet add package Grpc.Net.Client -Version 2.57.0
dotnet add package Microsoft.EntityFrameworkCore -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.Sqlite -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.SqlServer -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.InMemory -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.Design -Version 8.0.8
dotnet add package Polly -Version 8.0.0
```

Add these to the `.csproj` files of `GrpcRpcLib.Shared`, `GrpcRpcLib.Consumer`, and `GrpcRpcLib.Publisher`.

### Setup Instructions

1. **Clone the Repository:**
   ```bash
   git clone <repository-url>
   cd GrpcRpcLib
   ```

2. **Restore Packages:**
   ```bash
   dotnet restore
   ```

3. **Ensure appsettings.json Files:**
   - Create or update `appsettings.json` in both `GrpcRpcLib.Consumer` and `GrpcRpcLib.Publisher` projects (see [Configuration](#configuration) for details).
   - Ensure the files are copied to the output directory by adding to `.csproj`:
   ```xml
   <ItemGroup>
     <None Update="appsettings.json">
       <CopyToOutputDirectory>Always</CopyToOutputDirectory>
     </None>
   </ItemGroup>
   ```

4. **Apply Database Migrations (for Sqlite or SqlServer):**
   ```bash
   dotnet ef migrations add InitialCreate --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Consumer
   dotnet ef database update --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Consumer
   ```
   
   Repeat for the Publisher project:
   ```bash
   dotnet ef migrations add InitialCreate --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Publisher
   dotnet ef database update --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Publisher
   ```

### Configuration

#### Consumer Configuration
The Consumer service is configured via `appsettings.json` in the `GrpcRpcLib.Consumer` project. Example configuration:

```json
{
  "GrpcConsumer": {
    "Host": "localhost",
    "Port": 5000,
    "TimeoutSeconds": 30
  },
  "MessageStore": {
    "Type": "Sqlite",
    "ConnectionString": "Data Source=consumer.db",
    "Prefix": "Msg_"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**GrpcConsumer:**
- `Host`: The hostname or IP address (e.g., `localhost`).
- `Port`: The port for the gRPC server (default: 5000).
- `TimeoutSeconds`: Timeout for gRPC operations (in seconds).

**MessageStore**: See [Message Store Configuration](#message-store-configuration).

#### Publisher Configuration
The Publisher service is configured via `appsettings.json` in the `GrpcRpcLib.Publisher` project. Example configuration:

```json
{
  "GrpcPublisher": {
    "TargetHost": "localhost",
    "TargetPort": 5000
  },
  "MessageStore": {
    "Type": "Sqlite",
    "ConnectionString": "Data Source=publisher.db",
    "Prefix": "Msg_"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**GrpcPublisher:**
- `TargetHost`: The hostname or IP of the Consumer service (must match Consumer's `Host`).
- `TargetPort`: The port of the Consumer service (must match Consumer's `Port`).

**MessageStore**: See [Message Store Configuration](#message-store-configuration).

#### Message Store Configuration
Both Consumer and Publisher use a shared `MessageStore` configuration for database access. Supported database types:

- **InMemory**: For testing, no physical storage.
- **Sqlite**: Lightweight database, stores data in a `.db` file.
- **SqlServer**: Enterprise-grade database (requires SQL Server instance).

Example `MessageStore` configuration:
```json
"MessageStore": {
  "Type": "Sqlite",
  "ConnectionString": "Data Source=consumer.db",
  "Prefix": "Msg_"
}
```

**Parameters:**
- `Type`: One of `InMemory`, `Sqlite`, or `SqlServer`.
- `ConnectionString`:
  - For Sqlite: Path to the database file (e.g., `Data Source=consumer.db`).
  - For SqlServer: Connection string (e.g., `Server=localhost;Database=GrpcDb;Trusted_Connection=True;TrustServerCertificate=True`).
  - For InMemory: Leave empty or omit.
- `Prefix`: Table prefix (e.g., `Msg_` creates tables like `Msg_MessageEnvelopes`).

### Running the Project

1. **Run the Consumer:**
   ```bash
   cd GrpcRpcLib.Consumer
   dotnet run
   ```
   The Consumer should listen on `http://localhost:5000` (check console output).

2. **Run the Publisher:**
   ```bash
   cd GrpcRpcLib.Publisher
   dotnet run
   ```

3. **Run the Test Project (optional):**
   ```bash
   cd GrpcRpcLib.Test.Publisher
   dotnet run
   ```
   This sends test messages to the Consumer.

### Testing

1. **Test gRPC Service:**
   Use `grpcurl` to verify the Consumer service:
   ```bash
   grpcurl -plaintext localhost:5000 list
   ```
   Expected output: Lists services like `GrpcReceiver`.

2. **Test Database:**
   - For Sqlite: Use DB Browser for SQLite to check `consumer.db` or `publisher.db` for tables (`Msg_MessageEnvelopes`, `Msg_ServiceAddresses`).
   - For InMemory: Use breakpoints in `InMemoryMessageStore` to inspect data.

3. **Send Test Messages:**
   Use the `GrpcRpcLib.Test.Publisher` project to send messages and verify they are stored and processed.

### Troubleshooting

1. **HTTP/2 Errors (e.g., HTTP_1_1_REQUIRED):**
   - Ensure Kestrel is configured for HTTP/2 in `GrpcConsumerServerExtensions.cs`:
   ```csharp
   services.Configure<KestrelServerOptions>(options =>
   {
       options.ListenAnyIP(grpcConfiguration.Port, listenOptions =>
       {
           listenOptions.Protocols = HttpProtocols.Http2;
       });
   });
   ```
   - Use HTTP (not HTTPS) for testing to avoid certificate issues.
   - Check that the Consumer is running and listening on the correct port:
   ```bash
   netstat -an | findstr 5000
   ```

2. **Database Errors:**
   - Ensure migrations are applied (`dotnet ef database update`).
   - For InMemory, replace `MigrateAsync` with `EnsureCreatedAsync` in Initialize methods.

3. **Connection Issues:**
   - Verify `TargetHost` and `TargetPort` in Publisher's `appsettings.json` match Consumer's settings.
   - Open port 5000 in the firewall:
   ```bash
   netsh advfirewall firewall add rule name="Allow gRPC 5000" dir=in action=allow protocol=TCP localport=5000
   ```

### Contributing
Contributions are welcome! Please submit a pull request or open an issue on the repository.

### License
This project is licensed under the MIT License.

---

## فارسی

### بررسی اجمالی
GrpcRpcLib یک پروژه مبتنی بر .NET 8 است که یک سیستم پیام‌رسانی مبتنی بر gRPC با معماری Publisher-Consumer پیاده‌سازی می‌کند. این پروژه از چندین دیتابیس (Sqlite، SqlServer، InMemory) برای ذخیره‌سازی پیام‌ها پشتیبانی می‌کند و روشی قوی برای ارسال و پردازش پیام‌ها با استفاده از gRPC ارائه می‌دهد.

این فایل README دستورالعمل‌های دقیقی برای راه‌اندازی، پیکربندی، و اجرای پروژه ارائه می‌دهد، از جمله جزئیات تنظیمات `appsettings.json`، گزینه‌های دیتابیس، و تنظیمات gRPC.

### فهرست مطالب
- [ساختار پروژه](#ساختار-پروژه)
- [پیش‌نیازها](#پیش‌نیازها)
- [دستورالعمل‌های راه‌اندازی](#دستورالعمل‌های-راه‌اندازی)
- [پیکربندی](#پیکربندی)
  - [پیکربندی Consumer](#پیکربندی-consumer)
  - [پیکربندی Publisher](#پیکربندی-publisher)
  - [پیکربندی Message Store](#پیکربندی-message-store)
- [اجرای پروژه](#اجرای-پروژه)
- [تست](#تست)
- [عیب‌یابی](#عیب‌یابی)
- [مشارکت](#مشارکت)
- [لایسنس](#لایسنس)

### ساختار پروژه
راه‌حل شامل پروژه‌های زیر است:

- **GrpcRpcLib.Shared**: شامل منطق مشترک، از جمله `MessageDbContext`، `IMessageStore`، و تنظیمات مرتبط با دیتابیس (Sqlite، SqlServer، InMemory).
- **GrpcRpcLib.Consumer**: سرویس gRPC Consumer را پیاده‌سازی می‌کند که پیام‌ها را دریافت و پردازش می‌کند.
- **GrpcRpcLib.Publisher**: سرویس Publisher را پیاده‌سازی می‌کند که پیام‌ها را از طریق gRPC به Consumer ارسال می‌کند.
- **GrpcRpcLib.Test.Publisher**: یک پروژه آزمایشی برای شبیه‌سازی ارسال پیام‌ها از Publisher.

### پیش‌نیازها
مطمئن شوید موارد زیر نصب شده‌اند:

- **.NET 8 SDK**: از [Microsoft .NET](https://dotnet.microsoft.com/download) دانلود کنید.
- **ویژوال استودیو 2022 (یا بالاتر)** یا هر IDE که از .NET 8 پشتیبانی می‌کند (مثل Rider، VS Code).
- **SQLite (اختیاری، برای دیتابیس Sqlite)**: نصب sqlite3 یا یک مرورگر SQLite (مثل DB Browser for SQLite).
- **ابزارهای gRPC**: برای تست سرویس‌های gRPC، ابزار grpcurl را نصب کنید:
  ```bash
  go install github.com/fullstorydev/grpcurl/...@latest
  ```

#### پکیج‌های NuGet مورد نیاز
پکیج‌های زیر را به پروژه‌های مربوطه اضافه کنید:
```bash
dotnet add package Grpc.AspNetCore -Version 2.57.0
dotnet add package Grpc.Net.Client -Version 2.57.0
dotnet add package Microsoft.EntityFrameworkCore -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.Sqlite -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.SqlServer -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.InMemory -Version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.Design -Version 8.0.8
dotnet add package Polly -Version 8.0.0
```

این پکیج‌ها را به فایل‌های `.csproj` پروژه‌های `GrpcRpcLib.Shared`، `GrpcRpcLib.Consumer`، و `GrpcRpcLib.Publisher` اضافه کنید.

### دستورالعمل‌های راه‌اندازی

1. **کلون کردن مخزن:**
   ```bash
   git clone <repository-url>
   cd GrpcRpcLib
   ```

2. **بازگردانی پکیج‌ها:**
   ```bash
   dotnet restore
   ```

3. **بررسی فایل‌های appsettings.json:**
   - فایل `appsettings.json` را در پروژه‌های `GrpcRpcLib.Consumer` و `GrpcRpcLib.Publisher` ایجاد یا به‌روزرسانی کنید (جزئیات در [پیکربندی](#پیکربندی)).
   - مطمئن شوید فایل‌ها به دایرکتوری خروجی کپی می‌شوند:
   ```xml
   <ItemGroup>
     <None Update="appsettings.json">
       <CopyToOutputDirectory>Always</CopyToOutputDirectory>
     </None>
   </ItemGroup>
   ```

4. **اعمال Migrationهای دیتابیس (برای Sqlite یا SqlServer):**
   ```bash
   dotnet ef migrations add InitialCreate --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Consumer
   dotnet ef database update --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Consumer
   ```
   
   برای پروژه Publisher تکرار کنید:
   ```bash
   dotnet ef migrations add InitialCreate --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Publisher
   dotnet ef database update --project GrpcRpcLib.Shared --startup-project GrpcRpcLib.Publisher
   ```

### پیکربندی

#### پیکربندی Consumer
سرویس Consumer از طریق `appsettings.json` در پروژه `GrpcRpcLib.Consumer` پیکربندی می‌شود. نمونه تنظیمات:

```json
{
  "GrpcConsumer": {
    "Host": "localhost",
    "Port": 5000,
    "TimeoutSeconds": 30
  },
  "MessageStore": {
    "Type": "Sqlite",
    "ConnectionString": "Data Source=consumer.db",
    "Prefix": "Msg_"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**GrpcConsumer:**
- `Host`: نام هاست یا IP (مثل `localhost`).
- `Port`: پورت سرور gRPC (پیش‌فرض: 5000).
- `TimeoutSeconds`: زمان تایم‌اوت برای عملیات gRPC (به ثانیه).

**MessageStore**: به [پیکربندی Message Store](#پیکربندی-message-store) مراجعه کنید.

#### پیکربندی Publisher
سرویس Publisher از طریق `appsettings.json` در پروژه `GrpcRpcLib.Publisher` پیکربندی می‌شود. نمونه تنظیمات:

```json
{
  "GrpcPublisher": {
    "TargetHost": "localhost",
    "TargetPort": 5000
  },
  "MessageStore": {
    "Type": "Sqlite",
    "ConnectionString": "Data Source=publisher.db",
    "Prefix": "Msg_"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**GrpcPublisher:**
- `TargetHost`: نام هاست یا IP سرویس Consumer (باید با `Host` در Consumer مطابقت داشته باشد).
- `TargetPort`: پورت سرویس Consumer (باید با `Port` در Consumer مطابقت داشته باشد).

**MessageStore**: به [پیکربندی Message Store](#پیکربندی-message-store) مراجعه کنید.

#### پیکربندی Message Store
هر دو سرویس Consumer و Publisher از پیکربندی مشترک `MessageStore` برای دسترسی به دیتابیس استفاده می‌کنند. انواع دیتابیس‌های پشتیبانی‌شده:

- **InMemory**: برای تست، بدون ذخیره‌سازی فیزیکی.
- **Sqlite**: دیتابیس سبک، داده‌ها در فایل `.db` ذخیره می‌شوند.
- **SqlServer**: دیتابیس سازمانی (نیاز به نمونه SQL Server).

نمونه پیکربندی `MessageStore`:
```json
"MessageStore": {
  "Type": "Sqlite",
  "ConnectionString": "Data Source=consumer.db",
  "Prefix": "Msg_"
}
```

**پارامترها:**
- `Type`: یکی از `InMemory`، `Sqlite`، یا `SqlServer`.
- `ConnectionString`:
  - برای Sqlite: مسیر فایل دیتابیس (مثل `Data Source=consumer.db`).
  - برای SqlServer: رشته اتصال (مثل `Server=localhost;Database=GrpcDb;Trusted_Connection=True;TrustServerCertificate=True`).
  - برای InMemory: خالی بگذارید یا حذف کنید.
- `Prefix`: پیشوند جداول (مثل `Msg_` که جداول را به صورت `Msg_MessageEnvelopes` می‌سازد).

### اجرای پروژه

1. **اجرای Consumer:**
   ```bash
   cd GrpcRpcLib.Consumer
   dotnet run
   ```
   Consumer باید روی `http://localhost:5000` listen کند (خروجی کنسول را بررسی کنید).

2. **اجرای Publisher:**
   ```bash
   cd GrpcRpcLib.Publisher
   dotnet run
   ```

3. **اجرای پروژه تست (اختیاری):**
   ```bash
   cd GrpcRpcLib.Test.Publisher
   dotnet run
   ```
   این پروژه پیام‌های آزمایشی به Consumer ارسال می‌کند.

### تست

1. **تست سرویس gRPC:**
   با استفاده از `grpcurl` سرویس Consumer را بررسی کنید:
   ```bash
   grpcurl -plaintext localhost:5000 list
   ```
   خروجی مورد انتظار: لیست سرویس‌ها مثل `GrpcReceiver`.

2. **تست دیتابیس:**
   - برای Sqlite: از DB Browser for SQLite برای بررسی فایل‌های `consumer.db` یا `publisher.db` استفاده کنید (جداول: `Msg_MessageEnvelopes`، `Msg_ServiceAddresses`).
   - برای InMemory: از breakpoint در `InMemoryMessageStore` برای بررسی داده‌ها استفاده کنید.

3. **ارسال پیام‌های آزمایشی:**
   از پروژه `GrpcRpcLib.Test.Publisher` برای ارسال پیام‌ها استفاده کنید و بررسی کنید که پیام‌ها ذخیره و پردازش می‌شوند.

### عیب‌یابی

1. **خطاهای HTTP/2 (مثل HTTP_1_1_REQUIRED):**
   - مطمئن شوید Kestrel در `GrpcConsumerServerExtensions.cs` برای HTTP/2 تنظیم شده است:
   ```csharp
   services.Configure<KestrelServerOptions>(options =>
   {
       options.ListenAnyIP(grpcConfiguration.Port, listenOptions =>
       {
           listenOptions.Protocols = HttpProtocols.Http2;
       });
   });
   ```
   - برای تست، از HTTP (نه HTTPS) استفاده کنید تا مشکلات certificate حذف شوند.
   - بررسی کنید که Consumer روی پورت درست listen می‌کند:
   ```bash
   netstat -an | findstr 5000
   ```

2. **خطاهای دیتابیس:**
   - مطمئن شوید migrationها اعمال شده‌اند (`dotnet ef database update`).
   - برای InMemory، به جای `MigrateAsync` از `EnsureCreatedAsync` در متدهای Initialize استفاده کنید.

3. **مشکلات اتصال:**
   - بررسی کنید که `TargetHost` و `TargetPort` در `appsettings.json` Publisher با تنظیمات Consumer مطابقت دارند.
   - پورت 5000 را در فایروال باز کنید:
   ```bash
   netsh advfirewall firewall add rule name="Allow gRPC 5000" dir=in action=allow protocol=TCP localport=5000
   ```

### مشارکت
مشارکت‌ها استقبال می‌شوند! لطفاً یک Pull Request ارسال کنید یا یک Issue در مخزن باز کنید.

### لایسنس
این پروژه تحت لایسنس MIT منتشر شده است.