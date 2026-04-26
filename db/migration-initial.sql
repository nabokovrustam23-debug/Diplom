IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Branches] (
    [BranchId] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Address] nvarchar(500) NOT NULL,
    [Phone] nvarchar(20) NULL,
    [OpeningTime] time NOT NULL,
    [ClosingTime] time NOT NULL,
    CONSTRAINT [PK_Branches] PRIMARY KEY ([BranchId]),
    CONSTRAINT [CK_Branches_WorkHours] CHECK ([ClosingTime] > [OpeningTime])
);
GO

CREATE TABLE [Persona] (
    [PersonaId] int NOT NULL IDENTITY,
    [LastName] nvarchar(100) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [MiddleName] nvarchar(100) NULL,
    [Phone] nvarchar(20) NOT NULL,
    [Email] nvarchar(256) NULL,
    [BirthDate] date NULL,
    [Gender] int NULL,
    CONSTRAINT [PK_Persona] PRIMARY KEY ([PersonaId])
);
GO

CREATE TABLE [Services] (
    [ServiceId] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DurationMinutes] int NOT NULL,
    [Price] decimal(10,2) NOT NULL,
    CONSTRAINT [PK_Services] PRIMARY KEY ([ServiceId]),
    CONSTRAINT [CK_Services_Duration] CHECK ([DurationMinutes] > 0),
    CONSTRAINT [CK_Services_Price] CHECK ([Price] >= 0)
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] int NOT NULL IDENTITY,
    [PersonaId] int NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUsers_Persona_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [Persona] ([PersonaId])
);
GO

CREATE TABLE [Clients] (
    [ClientId] int NOT NULL IDENTITY,
    [PersonaId] int NOT NULL,
    [Source] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [FirstVisitDate] date NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSDATETIME()),
    CONSTRAINT [PK_Clients] PRIMARY KEY ([ClientId]),
    CONSTRAINT [FK_Clients_Persona_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [Persona] ([PersonaId])
);
GO

CREATE TABLE [Masters] (
    [MasterId] int NOT NULL IDENTITY,
    [PersonaId] int NOT NULL,
    [Position] nvarchar(100) NOT NULL,
    [HireDate] date NOT NULL,
    [AvatarPath] nvarchar(500) NULL,
    [Bio] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Masters] PRIMARY KEY ([MasterId]),
    CONSTRAINT [FK_Masters_Persona_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [Persona] ([PersonaId])
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] int NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Bookings] (
    [BookingId] int NOT NULL IDENTITY,
    [ClientId] int NOT NULL,
    [MasterId] int NOT NULL,
    [ServiceId] int NOT NULL,
    [BranchId] int NOT NULL,
    [StartDateTime] datetime2 NOT NULL,
    [DurationMinutes] int NOT NULL,
    [Status] int NOT NULL DEFAULT 1,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSDATETIME()),
    CONSTRAINT [PK_Bookings] PRIMARY KEY ([BookingId]),
    CONSTRAINT [CK_Bookings_Duration] CHECK ([DurationMinutes] > 0),
    CONSTRAINT [FK_Bookings_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([BranchId]),
    CONSTRAINT [FK_Bookings_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([ClientId]),
    CONSTRAINT [FK_Bookings_Masters_MasterId] FOREIGN KEY ([MasterId]) REFERENCES [Masters] ([MasterId]),
    CONSTRAINT [FK_Bookings_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([ServiceId])
);
GO

CREATE TABLE [MasterBranch] (
    [MasterId] int NOT NULL,
    [BranchId] int NOT NULL,
    CONSTRAINT [PK_MasterBranch] PRIMARY KEY ([MasterId], [BranchId]),
    CONSTRAINT [FK_MasterBranch_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([BranchId]) ON DELETE CASCADE,
    CONSTRAINT [FK_MasterBranch_Masters_MasterId] FOREIGN KEY ([MasterId]) REFERENCES [Masters] ([MasterId]) ON DELETE CASCADE
);
GO

CREATE TABLE [MasterService] (
    [MasterId] int NOT NULL,
    [ServiceId] int NOT NULL,
    CONSTRAINT [PK_MasterService] PRIMARY KEY ([MasterId], [ServiceId]),
    CONSTRAINT [FK_MasterService_Masters_MasterId] FOREIGN KEY ([MasterId]) REFERENCES [Masters] ([MasterId]) ON DELETE CASCADE,
    CONSTRAINT [FK_MasterService_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([ServiceId]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkSchedules] (
    [WorkScheduleId] int NOT NULL IDENTITY,
    [MasterId] int NOT NULL,
    [BranchId] int NOT NULL,
    [WorkDate] date NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [ScheduleType] int NOT NULL,
    CONSTRAINT [PK_WorkSchedules] PRIMARY KEY ([WorkScheduleId]),
    CONSTRAINT [CK_WorkSchedules_Times] CHECK ([EndTime] > [StartTime]),
    CONSTRAINT [FK_WorkSchedules_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([BranchId]),
    CONSTRAINT [FK_WorkSchedules_Masters_MasterId] FOREIGN KEY ([MasterId]) REFERENCES [Masters] ([MasterId])
);
GO

CREATE TABLE [Visits] (
    [VisitId] int NOT NULL IDENTITY,
    [BookingId] int NOT NULL,
    [TotalAmount] decimal(10,2) NOT NULL,
    [MasterNotes] nvarchar(2000) NULL,
    [CompletedAt] datetime2 NOT NULL DEFAULT (SYSDATETIME()),
    CONSTRAINT [PK_Visits] PRIMARY KEY ([VisitId]),
    CONSTRAINT [CK_Visits_Amount] CHECK ([TotalAmount] >= 0),
    CONSTRAINT [FK_Visits_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([BookingId])
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [IX_AspNetUsers_PersonaId] ON [AspNetUsers] ([PersonaId]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_Bookings_BranchId] ON [Bookings] ([BranchId]);
GO

CREATE INDEX [IX_Bookings_ClientId] ON [Bookings] ([ClientId]);
GO

CREATE INDEX [IX_Bookings_MasterId_StartDateTime] ON [Bookings] ([MasterId], [StartDateTime]);
GO

CREATE INDEX [IX_Bookings_ServiceId] ON [Bookings] ([ServiceId]);
GO

CREATE UNIQUE INDEX [IX_Clients_PersonaId] ON [Clients] ([PersonaId]);
GO

CREATE INDEX [IX_MasterBranch_BranchId] ON [MasterBranch] ([BranchId]);
GO

CREATE UNIQUE INDEX [IX_Masters_PersonaId] ON [Masters] ([PersonaId]);
GO

CREATE INDEX [IX_MasterService_ServiceId] ON [MasterService] ([ServiceId]);
GO

CREATE UNIQUE INDEX [IX_Persona_Phone] ON [Persona] ([Phone]);
GO

CREATE UNIQUE INDEX [IX_Visits_BookingId] ON [Visits] ([BookingId]);
GO

CREATE INDEX [IX_WorkSchedules_BranchId] ON [WorkSchedules] ([BranchId]);
GO

CREATE INDEX [IX_WorkSchedules_MasterId_WorkDate] ON [WorkSchedules] ([MasterId], [WorkDate]);
GO

CREATE UNIQUE INDEX [IX_WorkSchedules_MasterId_WorkDate_StartTime] ON [WorkSchedules] ([MasterId], [WorkDate], [StartTime]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260426001413_InitialCreate', N'8.0.26');
GO

COMMIT;
GO

