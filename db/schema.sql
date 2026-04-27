-- =============================================================================
-- Схема БД CRM-системы сети барбершопов «Тихий час»
-- СУБД: Microsoft SQL Server 2019+
-- Версия: черновик 1.0 (соответствует разделу 2.3 ПЗ — модель данных)
-- =============================================================================
-- Примечания:
--   1) Таблицы ASP.NET Core Identity (AspNetUsers, AspNetRoles, AspNetUserRoles,
--      AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims)
--      создаются автоматически при выполнении EF Core Migrations и в этом
--      скрипте не описываются. В таблицу AspNetUsers при этом добавляется
--      столбец PersonaId INT NOT NULL с уникальным индексом и FK на Persona.
--   2) Все первичные ключи — IDENTITY(1,1) INT, за исключением составных ключей
--      таблиц-связок MasterBranch и MasterService.
--   3) Удаление записей выполняется через ON DELETE NO ACTION; каскадное
--      удаление в MSSQL приводит к ошибке «multiple cascade paths» при
--      нескольких FK в одну таблицу (Bookings имеет четыре FK). Очистка
--      зависимых записей — на уровне приложения либо soft-delete.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================================================
-- Persona — Физическое лицо
-- Хранит общие персональные данные, на которые ссылаются специализированные
-- сущности User, Client, Master через UNIQUE FK (связь 1:1).
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
    OpeningTime  TIME           NOT NULL,
    ClosingTime  TIME           NOT NULL,
    CONSTRAINT PK_Branches           PRIMARY KEY (BranchId),
    CONSTRAINT CK_Branches_WorkHours CHECK (ClosingTime > OpeningTime)
);
GO

-- =============================================================================
-- Services — Услуги прайс-листа
-- =============================================================================
CREATE TABLE dbo.Services (
    ServiceId        INT             IDENTITY(1, 1) NOT NULL,
    Name             NVARCHAR(200)   NOT NULL,
    Description      NVARCHAR(1000)  NULL,
    DurationMinutes  INT             NOT NULL,
    Price            DECIMAL(10, 2)  NOT NULL,
    CONSTRAINT PK_Services          PRIMARY KEY (ServiceId),
    CONSTRAINT CK_Services_Duration CHECK (DurationMinutes > 0),
    CONSTRAINT CK_Services_Price    CHECK (Price >= 0)
);
GO

-- =============================================================================
-- Masters — Мастера-барберы
-- Общие персональные данные — в Persona; специфичные для роли — здесь.
-- =============================================================================
CREATE TABLE dbo.Masters (
    MasterId    INT            IDENTITY(1, 1) NOT NULL,
    PersonaId   INT            NOT NULL,
    Position    NVARCHAR(100)  NOT NULL,
    HireDate    DATE           NOT NULL,
    AvatarPath  NVARCHAR(500)  NULL,
    Bio         NVARCHAR(2000) NULL,
    IsActive    BIT            NOT NULL CONSTRAINT DF_Masters_IsActive DEFAULT (1),
    CONSTRAINT PK_Masters           PRIMARY KEY (MasterId),
    CONSTRAINT UQ_Masters_PersonaId UNIQUE (PersonaId),
    CONSTRAINT FK_Masters_Persona   FOREIGN KEY (PersonaId)
        REFERENCES dbo.Persona (PersonaId) ON DELETE NO ACTION
);
GO

-- =============================================================================
-- Clients — Клиенты
-- Может существовать «гостевой» клиент (без привязки к AspNetUsers) —
-- в этом случае в Persona заполнены только ФИО и Phone, учётной записи
-- Identity нет. При последующей регистрации эта же Persona связывается
-- с AspNetUsers, и история визитов «прилипает» автоматически.
-- =============================================================================
CREATE TABLE dbo.Clients (
    ClientId        INT            IDENTITY(1, 1) NOT NULL,
    PersonaId       INT            NOT NULL,
    Source          NVARCHAR(100)  NULL,
    Notes           NVARCHAR(1000) NULL,
    FirstVisitDate  DATE           NULL,
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_Clients_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Clients           PRIMARY KEY (ClientId),
    CONSTRAINT UQ_Clients_PersonaId UNIQUE (PersonaId),
    CONSTRAINT FK_Clients_Persona   FOREIGN KEY (PersonaId)
        REFERENCES dbo.Persona (PersonaId) ON DELETE NO ACTION
);
GO

-- =============================================================================
-- MasterBranch — Связка «мастер ↔ филиал» (N:M)
-- Составной первичный ключ.
-- =============================================================================
CREATE TABLE dbo.MasterBranch (
    MasterId  INT NOT NULL,
    BranchId  INT NOT NULL,
    CONSTRAINT PK_MasterBranch          PRIMARY KEY (MasterId, BranchId),
    CONSTRAINT FK_MasterBranch_Master   FOREIGN KEY (MasterId)
        REFERENCES dbo.Masters  (MasterId) ON DELETE CASCADE,
    CONSTRAINT FK_MasterBranch_Branch   FOREIGN KEY (BranchId)
        REFERENCES dbo.Branches (BranchId) ON DELETE CASCADE
);
GO

-- =============================================================================
-- MasterService — Связка «мастер ↔ услуга» (N:M)
-- =============================================================================
CREATE TABLE dbo.MasterService (
    MasterId   INT NOT NULL,
    ServiceId  INT NOT NULL,
    CONSTRAINT PK_MasterService         PRIMARY KEY (MasterId, ServiceId),
    CONSTRAINT FK_MasterService_Master  FOREIGN KEY (MasterId)
        REFERENCES dbo.Masters  (MasterId)  ON DELETE CASCADE,
    CONSTRAINT FK_MasterService_Service FOREIGN KEY (ServiceId)
        REFERENCES dbo.Services (ServiceId) ON DELETE CASCADE
);
GO

-- =============================================================================
-- WorkSchedules — Рабочий график мастера
-- Смена, обед, выходной, отпуск, больничный.
-- =============================================================================
CREATE TABLE dbo.WorkSchedules (
    WorkScheduleId  INT           IDENTITY(1, 1) NOT NULL,
    MasterId        INT           NOT NULL,
    BranchId        INT           NOT NULL,
    WorkDate        DATE          NOT NULL,
    StartTime       TIME          NOT NULL,
    EndTime         TIME          NOT NULL,
    ScheduleType    NVARCHAR(20)  NOT NULL,
    CONSTRAINT PK_WorkSchedules         PRIMARY KEY (WorkScheduleId),
    CONSTRAINT FK_WorkSchedules_Master  FOREIGN KEY (MasterId)
        REFERENCES dbo.Masters  (MasterId)  ON DELETE NO ACTION,
    CONSTRAINT FK_WorkSchedules_Branch  FOREIGN KEY (BranchId)
        REFERENCES dbo.Branches (BranchId) ON DELETE NO ACTION,
    CONSTRAINT CK_WorkSchedules_Type    CHECK (ScheduleType IN
        (N'Work', N'Lunch', N'DayOff', N'Vacation', N'SickLeave')),
    CONSTRAINT CK_WorkSchedules_Times   CHECK (EndTime > StartTime),
    CONSTRAINT UQ_WorkSchedules_Slot    UNIQUE (MasterId, WorkDate, StartTime)
);
GO

CREATE INDEX IX_WorkSchedules_Master_Date
    ON dbo.WorkSchedules (MasterId, WorkDate);
GO

-- =============================================================================
-- Bookings — Записи на приём
-- DurationMinutes — «моментальный снимок» длительности услуги на момент
-- создания записи (чтобы последующее изменение прайс-листа не ломало
-- существующие записи).
-- =============================================================================
CREATE TABLE dbo.Bookings (
    BookingId        INT            IDENTITY(1, 1) NOT NULL,
    ClientId         INT            NOT NULL,
    MasterId         INT            NOT NULL,
    ServiceId        INT            NOT NULL,
    BranchId         INT            NOT NULL,
    StartDateTime    DATETIME2      NOT NULL,
    DurationMinutes  INT            NOT NULL,
    Status           NVARCHAR(20)   NOT NULL CONSTRAINT DF_Bookings_Status   DEFAULT (N'Created'),
    CreatedAt        DATETIME2      NOT NULL CONSTRAINT DF_Bookings_Created  DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Bookings            PRIMARY KEY (BookingId),
    CONSTRAINT FK_Bookings_Client     FOREIGN KEY (ClientId)
        REFERENCES dbo.Clients  (ClientId)  ON DELETE NO ACTION,
    CONSTRAINT FK_Bookings_Master     FOREIGN KEY (MasterId)
        REFERENCES dbo.Masters  (MasterId)  ON DELETE NO ACTION,
    CONSTRAINT FK_Bookings_Service    FOREIGN KEY (ServiceId)
        REFERENCES dbo.Services (ServiceId) ON DELETE NO ACTION,
    CONSTRAINT FK_Bookings_Branch     FOREIGN KEY (BranchId)
        REFERENCES dbo.Branches (BranchId) ON DELETE NO ACTION,
    CONSTRAINT CK_Bookings_Duration   CHECK (DurationMinutes > 0),
    CONSTRAINT CK_Bookings_Status     CHECK (Status IN
        (N'Created', N'Confirmed', N'Cancelled', N'Completed', N'NoShow'))
);
GO

-- Индекс для быстрого поиска конфликтов и вывода расписания мастера на день.
CREATE INDEX IX_Bookings_Master_Start
    ON dbo.Bookings (MasterId, StartDateTime);
GO

CREATE INDEX IX_Bookings_Client
    ON dbo.Bookings (ClientId);
GO

-- =============================================================================
-- Visits — Факт оказания услуги (1:1 с Bookings)
-- Создаётся при переводе записи в статус 'Completed'.
-- =============================================================================
CREATE TABLE dbo.Visits (
    VisitId       INT             IDENTITY(1, 1) NOT NULL,
    BookingId     INT             NOT NULL,
    TotalAmount   DECIMAL(10, 2)  NOT NULL,
    MasterNotes   NVARCHAR(2000)  NULL,
    CompletedAt   DATETIME2       NOT NULL CONSTRAINT DF_Visits_CompletedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Visits           PRIMARY KEY (VisitId),
    CONSTRAINT UQ_Visits_BookingId UNIQUE (BookingId),
    CONSTRAINT FK_Visits_Booking   FOREIGN KEY (BookingId)
        REFERENCES dbo.Bookings (BookingId) ON DELETE NO ACTION,
    CONSTRAINT CK_Visits_Amount    CHECK (TotalAmount >= 0)
);
GO

-- =============================================================================
-- Примечание по связи с AspNetUsers (ASP.NET Core Identity)
-- После выполнения EF Core миграции «InitialCreate» в таблицу AspNetUsers
-- добавляется столбец и ограничения:
--
--     ALTER TABLE dbo.AspNetUsers
--         ADD PersonaId INT NOT NULL;
--
--     CREATE UNIQUE INDEX UQ_AspNetUsers_PersonaId
--         ON dbo.AspNetUsers (PersonaId);
--
--     ALTER TABLE dbo.AspNetUsers
--         ADD CONSTRAINT FK_AspNetUsers_Persona
--         FOREIGN KEY (PersonaId) REFERENCES dbo.Persona (PersonaId)
--         ON DELETE NO ACTION;
--
-- Это обеспечивает связь 1:1 между учётной записью Identity и Persona.
-- =============================================================================
