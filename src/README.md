# Исходный код CRM-системы «Тихий час»

ASP.NET Core 8 + EF Core 8. Целевая СУБД для ВКР — MS SQL Server 2019,
локальный demo/dev-запуск по умолчанию использует SQLite.

## Структура решения

```
BarbershopCrm.sln
├── src/
│   ├── BarbershopCrm.Domain/         ← классы сущностей (POCO), перечисления
│   ├── BarbershopCrm.Infrastructure/ ← ApplicationDbContext, ApplicationUser, сидеры
│   └── BarbershopCrm.Web/            ← Razor Pages, ASP.NET Core Identity, Program.cs
└── tests/
    └── BarbershopCrm.Tests/          ← xUnit + Moq + EFCore.InMemory
```

| Проект | Назначение | Зависимости |
|---|---|---|
| `BarbershopCrm.Domain` | Чистая модель предметной области: 11 сущностей (`Persona`, `Branch`, `Service`, `Master`, `Client`, `MasterBranch`, `MasterService`, `WorkSchedule`, `Booking`, `Visit`) и перечисления (`Gender`, `BookingStatus`, `ScheduleType`). Не зависит от EF Core / Identity. | — |
| `BarbershopCrm.Infrastructure` | `ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>` с Fluent-конфигурациями всех сущностей, `ApplicationUser` (расширяет Identity, добавляет FK на `Persona`). | EF Core, EF Core Sqlite/SqlServer, AspNetCore.Identity.EntityFrameworkCore, Domain |
| `BarbershopCrm.Web` | Razor Pages + Identity UI. `Program.cs` подключает `ApplicationDbContext` через провайдер, указанный в конфигурации (`Sqlite` или `SqlServer`). | Infrastructure, Domain |
| `BarbershopCrm.Tests` | Юнит-тесты доменной логики и сервисов. | xUnit, Moq, EFCore.InMemory, Web/Infrastructure/Domain |

## Команды

```bash
# Сборка и тесты
dotnet build
dotnet test

# Миграции (запускаются из корня репо)
dotnet ef migrations add <Name> \
    --project src/BarbershopCrm.Infrastructure \
    --startup-project src/BarbershopCrm.Web

dotnet ef database update \
    --project src/BarbershopCrm.Infrastructure \
    --startup-project src/BarbershopCrm.Web

# Экспорт DDL текущей миграции в SQL-скрипт
dotnet ef migrations script \
    --project src/BarbershopCrm.Infrastructure \
    --startup-project src/BarbershopCrm.Web \
    -o db/migration-initial.sql

# Запуск веб-приложения
dotnet run --project src/BarbershopCrm.Web
```

## Подключение к БД

`appsettings.json` по умолчанию использует SQLite:

```
Database:Provider=Sqlite
ConnectionStrings:DefaultConnection=Data Source=barbershop.db
```

Для целевого MSSQL-окружения переопредели провайдер и строку подключения через
переменные окружения:

```bash
export Database__Provider=SqlServer
export ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=BarbershopCrm;Trusted_Connection=True;TrustServerCertificate=True'
```

Демо-учётка владельца также задаётся конфигурацией: `DemoOwner:Email` и
`DemoOwner:Password`. Для защиты можно оставить значения из `appsettings.json`,
для реального развёртывания их нужно переопределить переменными окружения.

## Соответствие разделам ПЗ

| Артефакт | Раздел |
|---|---|
| `Domain/Entities/*.cs` | 2.3 «Проектирование модели данных» (классы сущностей) |
| `Infrastructure/Data/ApplicationDbContext.cs` | 3.2 «Разработка БД в среде СУБД» (Fluent-конфигурация) |
| `db/migration-initial.sql` | 3.2 (DDL, сгенерированный EF Core) |
| `Web/Pages/*` | 3.3 «Описание приложения» (UI) |
| `tests/BarbershopCrm.Tests/*` | 4.1 «Модульное тестирование» |
