# Приложение В
## (обязательное)
### Структура базы данных

В настоящем приложении приведена структура базы данных CRM-системы сети барбершопов «Тихий час»: ER-диаграмма физической модели и DDL-скрипт создания таблиц, ограничений и индексов на языке Transact-SQL.

База данных содержит 11 таблиц предметной области и 7 системных таблиц ASP.NET Core Identity. Системные таблицы создаются автоматически миграциями EF Core и в DDL ниже не приводятся; в таблицу `AspNetUsers` при этом добавляется столбец `PersonaId` с уникальным индексом и внешним ключом на `Persona`.

## В.1 Перечень таблиц предметной области

Перечень таблиц с краткой характеристикой назначения приведён в таблице В.1.

Таблица В.1 — Перечень таблиц базы данных

| Таблица            | Назначение                                                              |
|--------------------|-------------------------------------------------------------------------|
| `Persona`          | Физическое лицо: общие персональные данные (ФИО, телефон, e-mail, пол)  |
| `Branches`         | Филиалы сети с адресом, графиком работы и контактами                    |
| `Services`         | Прайс-лист услуг сети: наименование, длительность, цена, описание       |
| `Masters`          | Мастера-барберы: ссылка на `Persona` + специфичные атрибуты             |
| `MasterBranch`     | Связка «мастер–филиал» (M:N)                                            |
| `MasterService`    | Связка «мастер–услуга» с возможным переопределением длительности        |
| `WorkSchedule`     | Расписание работы мастера в филиале на день/неделю                      |
| `Bookings`         | Записи (онлайн и офлайн) на услуги в выбранный филиал к мастеру         |
| `Visits`           | История фактических визитов клиента (отдельно от записи)                |
| `Clients`          | Клиенты: ссылка на `Persona` + атрибуты CRM (последний визит, чек)      |

## В.2 ER-диаграмма

ER-диаграмма физической модели данных представлена на рисунке 2.1 в подразделе 2.4 пояснительной записки.

## В.3 DDL-скрипт создания базы данных

Скрипт ниже соответствует полной схеме БД и может быть выполнен на чистом экземпляре MSSQL 2019+ для развёртывания структуры.

```sql
-- =============================================================================
-- Persona — Физическое лицо
-- =============================================================================
CREATE TABLE dbo.Persona (
    PersonaId    INT           IDENTITY(1, 1) NOT NULL,
    LastName     NVARCHAR(100) NOT NULL,
    FirstName    NVARCHAR(100) NOT NULL,
    MiddleName   NVARCHAR(100) NULL,
    Phone        NVARCHAR(20)  NOT NULL,
    Email        NVARCHAR(256) NULL,
    BirthDate    DATE          NULL,
    Gender       NCHAR(1)      NULL,
    CONSTRAINT PK_Persona        PRIMARY KEY (PersonaId),
    CONSTRAINT UQ_Persona_Phone  UNIQUE (Phone),
    CONSTRAINT CK_Persona_Gender CHECK (Gender IS NULL OR Gender IN (N'М', N'Ж'))
);
GO

-- =============================================================================
-- Branches — Филиалы
-- =============================================================================
CREATE TABLE dbo.Branches (
    BranchId     INT            IDENTITY(1, 1) NOT NULL,
    Name         NVARCHAR(200)  NOT NULL,
    Address      NVARCHAR(500)  NOT NULL,
    Phone        NVARCHAR(20)   NULL,
    OpenTime     TIME(0)        NOT NULL,
    CloseTime    TIME(0)        NOT NULL,
    IsActive     BIT            NOT NULL CONSTRAINT DF_Branches_IsActive DEFAULT (1),
    CONSTRAINT PK_Branches PRIMARY KEY (BranchId),
    CONSTRAINT CK_Branches_Time CHECK (OpenTime < CloseTime)
);
GO

-- =============================================================================
-- Services — Услуги
-- =============================================================================
CREATE TABLE dbo.Services (
    ServiceId        INT           IDENTITY(1, 1) NOT NULL,
    Name             NVARCHAR(200) NOT NULL,
    Description      NVARCHAR(MAX) NULL,
    DurationMinutes  INT           NOT NULL,
    Price            DECIMAL(10,2) NOT NULL,
    IsActive         BIT           NOT NULL CONSTRAINT DF_Services_IsActive DEFAULT (1),
    CONSTRAINT PK_Services PRIMARY KEY (ServiceId),
    CONSTRAINT CK_Services_Duration CHECK (DurationMinutes BETWEEN 5 AND 480),
    CONSTRAINT CK_Services_Price CHECK (Price >= 0)
);
GO

-- =============================================================================
-- Masters — Мастера-барберы
-- =============================================================================
CREATE TABLE dbo.Masters (
    MasterId    INT           IDENTITY(1, 1) NOT NULL,
    PersonaId   INT           NOT NULL,
    Position    NVARCHAR(100) NULL,
    HireDate    DATE          NULL,
    AvatarPath  NVARCHAR(500) NULL,
    Bio         NVARCHAR(MAX) NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_Masters_IsActive DEFAULT (1),
    CONSTRAINT PK_Masters             PRIMARY KEY (MasterId),
    CONSTRAINT UQ_Masters_PersonaId   UNIQUE (PersonaId),
    CONSTRAINT FK_Masters_Persona     FOREIGN KEY (PersonaId)
        REFERENCES dbo.Persona (PersonaId) ON DELETE NO ACTION
);
GO

-- =============================================================================
-- MasterBranch — Связка мастер–филиал
-- =============================================================================
CREATE TABLE dbo.MasterBranch (
    MasterId  INT NOT NULL,
    BranchId  INT NOT NULL,
    CONSTRAINT PK_MasterBranch        PRIMARY KEY (MasterId, BranchId),
    CONSTRAINT FK_MasterBranch_Master FOREIGN KEY (MasterId)
        REFERENCES dbo.Masters (MasterId) ON DELETE CASCADE,
    CONSTRAINT FK_MasterBranch_Branch FOREIGN KEY (BranchId)
        REFERENCES dbo.Branches (BranchId) ON DELETE CASCADE
);
GO

-- =============================================================================
-- MasterService — Связка мастер–услуга
-- =============================================================================
CREATE TABLE dbo.MasterService (
    MasterId         INT NOT NULL,
    ServiceId        INT NOT NULL,
    DurationOverride INT NULL,
    CONSTRAINT PK_MasterService          PRIMARY KEY (MasterId, ServiceId),
    CONSTRAINT FK_MasterService_Master   FOREIGN KEY (MasterId)
        REFERENCES dbo.Masters (MasterId) ON DELETE CASCADE,
    CONSTRAINT FK_MasterService_Service  FOREIGN KEY (ServiceId)
        REFERENCES dbo.Services (ServiceId) ON DELETE CASCADE,
    CONSTRAINT CK_MasterService_Duration CHECK (DurationOverride IS NULL OR DurationOverride BETWEEN 5 AND 480)
);
GO

-- =============================================================================
-- WorkSchedule — Расписание мастера в филиале
-- =============================================================================
CREATE TABLE dbo.WorkSchedule (
    ScheduleId  INT       IDENTITY(1, 1) NOT NULL,
    MasterId    INT       NOT NULL,
    BranchId    INT       NOT NULL,
    WorkDate    DATE      NOT NULL,
    StartTime   TIME(0)   NOT NULL,
    EndTime     TIME(0)   NOT NULL,
    CONSTRAINT PK_WorkSchedule          PRIMARY KEY (ScheduleId),
    CONSTRAINT UQ_WorkSchedule          UNIQUE (MasterId, BranchId, WorkDate),
    CONSTRAINT FK_WorkSchedule_Master   FOREIGN KEY (MasterId)
        REFERENCES dbo.Masters (MasterId) ON DELETE NO ACTION,
    CONSTRAINT FK_WorkSchedule_Branch   FOREIGN KEY (BranchId)
        REFERENCES dbo.Branches (BranchId) ON DELETE NO ACTION,
    CONSTRAINT CK_WorkSchedule_Time     CHECK (StartTime < EndTime)
);
GO

-- =============================================================================
-- Clients — Клиенты
-- =============================================================================
CREATE TABLE dbo.Clients (
    ClientId      INT           IDENTITY(1, 1) NOT NULL,
    PersonaId     INT           NOT NULL,
    LastVisitDate DATETIME2(0)  NULL,
    AverageCheck  DECIMAL(10,2) NULL,
    CONSTRAINT PK_Clients           PRIMARY KEY (ClientId),
    CONSTRAINT UQ_Clients_PersonaId UNIQUE (PersonaId),
    CONSTRAINT FK_Clients_Persona   FOREIGN KEY (PersonaId)
        REFERENCES dbo.Persona (PersonaId) ON DELETE NO ACTION
);
GO

-- =============================================================================
-- Bookings — Записи (онлайн и офлайн)
-- =============================================================================
CREATE TABLE dbo.Bookings (
    BookingId   INT           IDENTITY(1, 1) NOT NULL,
    ClientId    INT           NULL,
    BranchId    INT           NOT NULL,
    MasterId    INT           NOT NULL,
    ServiceId   INT           NOT NULL,
    StartAt     DATETIME2(0)  NOT NULL,
    EndAt       DATETIME2(0)  NOT NULL,
    Status      INT           NOT NULL CONSTRAINT DF_Bookings_Status DEFAULT (0),
    GuestName   NVARCHAR(200) NULL,
    GuestPhone  NVARCHAR(20)  NULL,
    Notes       NVARCHAR(MAX) NULL,
    CreatedAt   DATETIME2(0)  NOT NULL CONSTRAINT DF_Bookings_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Bookings           PRIMARY KEY (BookingId),
    CONSTRAINT FK_Bookings_Client    FOREIGN KEY (ClientId)  REFERENCES dbo.Clients  (ClientId)  ON DELETE NO ACTION,
    CONSTRAINT FK_Bookings_Branch    FOREIGN KEY (BranchId)  REFERENCES dbo.Branches (BranchId)  ON DELETE NO ACTION,
    CONSTRAINT FK_Bookings_Master    FOREIGN KEY (MasterId)  REFERENCES dbo.Masters  (MasterId)  ON DELETE NO ACTION,
    CONSTRAINT FK_Bookings_Service   FOREIGN KEY (ServiceId) REFERENCES dbo.Services (ServiceId) ON DELETE NO ACTION,
    CONSTRAINT CK_Bookings_Time      CHECK (StartAt < EndAt),
    CONSTRAINT CK_Bookings_GuestOrClient CHECK (
        (ClientId IS NOT NULL) OR (GuestName IS NOT NULL AND GuestPhone IS NOT NULL)
    )
);
GO

-- =============================================================================
-- Visits — История фактических визитов
-- =============================================================================
CREATE TABLE dbo.Visits (
    VisitId     INT           IDENTITY(1, 1) NOT NULL,
    BookingId   INT           NOT NULL,
    ClientId    INT           NULL,
    VisitDate   DATETIME2(0)  NOT NULL,
    TotalAmount DECIMAL(10,2) NOT NULL,
    Notes       NVARCHAR(MAX) NULL,
    CONSTRAINT PK_Visits         PRIMARY KEY (VisitId),
    CONSTRAINT FK_Visits_Booking FOREIGN KEY (BookingId)
        REFERENCES dbo.Bookings (BookingId) ON DELETE NO ACTION,
    CONSTRAINT FK_Visits_Client  FOREIGN KEY (ClientId)
        REFERENCES dbo.Clients (ClientId) ON DELETE NO ACTION,
    CONSTRAINT CK_Visits_Total   CHECK (TotalAmount >= 0)
);
GO
```

## В.4 Индексы

Для оптимизации частых запросов созданы дополнительные некластеризованные индексы:

```sql
CREATE INDEX IX_Bookings_BranchMasterStart   ON dbo.Bookings (BranchId, MasterId, StartAt);
CREATE INDEX IX_Bookings_ClientStart         ON dbo.Bookings (ClientId, StartAt) WHERE ClientId IS NOT NULL;
CREATE INDEX IX_WorkSchedule_BranchDate      ON dbo.WorkSchedule (BranchId, WorkDate);
CREATE INDEX IX_Visits_ClientDate            ON dbo.Visits (ClientId, VisitDate) WHERE ClientId IS NOT NULL;
GO
```

## В.5 Особенности реализации

– Каскадное удаление в MSSQL запрещено для таблицы `Bookings`, так как она содержит четыре внешних ключа в разные таблицы — комбинация каскадных правил приводит к ошибке *multiple cascade paths*. Удаление зависимых записей выполняется на уровне приложения либо через soft-delete (сохранение записи со статусом `Cancelled`).

– Длина строковых полей выбрана из расчёта поддержки кириллицы и латиницы в одном экземпляре сервера, тип данных — `NVARCHAR` (Unicode UCS-2).

– Для гостевых записей (без аккаунта пользователя) поля `GuestName` и `GuestPhone` обязательны, иначе запись отклоняется ограничением `CK_Bookings_GuestOrClient`. После регистрации гостя его записи могут быть привязаны к `Clients` через UPDATE `ClientId` по совпадению телефона.

– Тип `DECIMAL(10,2)` выбран для денежных сумм, обеспечивает точность до копейки в диапазоне до 99 999 999,99 руб., что достаточно для предметной области.
