using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Infrastructure.Data;

/// <summary>
/// Контекст БД CRM-системы. Объединяет таблицы доменной модели и
/// стандартные таблицы ASP.NET Core Identity (AspNetUsers, AspNetRoles и т.п.).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Master> Masters => Set<Master>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<MasterBranch> MasterBranches => Set<MasterBranch>();
    public DbSet<MasterService> MasterServices => Set<MasterService>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Visit> Visits => Set<Visit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigurePersona(builder);
        ConfigureBranch(builder);
        ConfigureService(builder);
        ConfigureMaster(builder);
        ConfigureClient(builder);
        ConfigureMasterBranch(builder);
        ConfigureMasterService(builder);
        ConfigureWorkSchedule(builder);
        ConfigureBooking(builder);
        ConfigureVisit(builder);
        ConfigureApplicationUser(builder);
    }

    private static void ConfigurePersona(ModelBuilder b)
    {
        b.Entity<Persona>(e =>
        {
            e.ToTable("Persona");
            e.HasKey(x => x.PersonaId);
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.MiddleName).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Gender).HasConversion<int?>();
            e.HasIndex(x => x.Phone).IsUnique();
        });
    }

    private static void ConfigureBranch(ModelBuilder b)
    {
        b.Entity<Branch>(e =>
        {
            e.ToTable("Branches", t => t.HasCheckConstraint("CK_Branches_WorkHours", "ClosingTime > OpeningTime"));
            e.HasKey(x => x.BranchId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Address).HasMaxLength(500).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20);
        });
    }

    private static void ConfigureService(ModelBuilder b)
    {
        b.Entity<Service>(e =>
        {
            e.ToTable("Services", t =>
            {
                t.HasCheckConstraint("CK_Services_Duration", "DurationMinutes > 0");
                t.HasCheckConstraint("CK_Services_Price", "Price >= 0");
            });
            e.HasKey(x => x.ServiceId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Price).HasColumnType("decimal(10, 2)");
            e.Property(x => x.DisplayOrder).HasDefaultValue(0);
        });
    }

    private static void ConfigureMaster(ModelBuilder b)
    {
        b.Entity<Master>(e =>
        {
            e.ToTable("Masters");
            e.HasKey(x => x.MasterId);
            e.Property(x => x.Position).HasMaxLength(100).IsRequired();
            e.Property(x => x.AvatarPath).HasMaxLength(500);
            e.Property(x => x.Bio).HasMaxLength(2000);
            e.Property(x => x.IsActive).HasDefaultValue(true);

            e.HasIndex(x => x.PersonaId).IsUnique();
            e.HasOne(x => x.Persona)
                .WithOne(p => p.Master!)
                .HasForeignKey<Master>(x => x.PersonaId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureClient(ModelBuilder b)
    {
        b.Entity<Client>(e =>
        {
            e.ToTable("Clients");
            e.HasKey(x => x.ClientId);
            e.Property(x => x.Source).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasIndex(x => x.PersonaId).IsUnique();
            e.HasOne(x => x.Persona)
                .WithOne(p => p.Client!)
                .HasForeignKey<Client>(x => x.PersonaId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureMasterBranch(ModelBuilder b)
    {
        b.Entity<MasterBranch>(e =>
        {
            e.ToTable("MasterBranch");
            e.HasKey(x => new { x.MasterId, x.BranchId });

            e.HasOne(x => x.Master)
                .WithMany(m => m.MasterBranches)
                .HasForeignKey(x => x.MasterId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Branch)
                .WithMany(br => br.MasterBranches)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMasterService(ModelBuilder b)
    {
        b.Entity<MasterService>(e =>
        {
            e.ToTable("MasterService");
            e.HasKey(x => new { x.MasterId, x.ServiceId });

            e.HasOne(x => x.Master)
                .WithMany(m => m.MasterServices)
                .HasForeignKey(x => x.MasterId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Service)
                .WithMany(s => s.MasterServices)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureWorkSchedule(ModelBuilder b)
    {
        b.Entity<WorkSchedule>(e =>
        {
            e.ToTable("WorkSchedules", t =>
                t.HasCheckConstraint("CK_WorkSchedules_Times", "EndTime > StartTime"));
            e.HasKey(x => x.WorkScheduleId);
            e.Property(x => x.ScheduleType).HasConversion<int>();

            e.HasIndex(x => new { x.MasterId, x.WorkDate, x.StartTime }).IsUnique();
            e.HasIndex(x => new { x.MasterId, x.WorkDate });

            e.HasOne(x => x.Master)
                .WithMany(m => m.WorkSchedules)
                .HasForeignKey(x => x.MasterId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Branch)
                .WithMany(br => br.WorkSchedules)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureBooking(ModelBuilder b)
    {
        b.Entity<Booking>(e =>
        {
            e.ToTable("Bookings", t =>
                t.HasCheckConstraint("CK_Bookings_Duration", "DurationMinutes > 0"));
            e.HasKey(x => x.BookingId);
            e.Property(x => x.Status).HasConversion<int>().HasDefaultValue(BookingStatus.Created);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Property(x => x.Wishes).HasMaxLength(500);

            e.HasIndex(x => new { x.MasterId, x.StartDateTime });
            e.HasIndex(x => x.ClientId);

            e.HasOne(x => x.Client)
                .WithMany(c => c.Bookings)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Master)
                .WithMany(m => m.Bookings)
                .HasForeignKey(x => x.MasterId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Service)
                .WithMany(s => s.Bookings)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Branch)
                .WithMany(br => br.Bookings)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureVisit(ModelBuilder b)
    {
        b.Entity<Visit>(e =>
        {
            e.ToTable("Visits", t =>
                t.HasCheckConstraint("CK_Visits_Amount", "TotalAmount >= 0"));
            e.HasKey(x => x.VisitId);
            e.Property(x => x.TotalAmount).HasColumnType("decimal(10, 2)");
            e.Property(x => x.MasterNotes).HasMaxLength(2000);
            e.Property(x => x.CompletedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasOne(x => x.Booking)
                .WithOne(bk => bk.Visit!)
                .HasForeignKey<Visit>(x => x.BookingId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureApplicationUser(ModelBuilder b)
    {
        b.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(x => x.PersonaId).IsUnique();
            e.HasOne(x => x.Persona)
                .WithOne()
                .HasForeignKey<ApplicationUser>(x => x.PersonaId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
