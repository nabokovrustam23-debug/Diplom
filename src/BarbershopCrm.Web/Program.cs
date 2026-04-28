using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Infrastructure.Services;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        // Политика пароля: минимум 8 символов, обязательны цифра и буква
        // в разном регистре. Спецсимвол не требуем (часть сотрудников вводит
        // пароль с экранной клавиатуры в админке филиала).
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Блокировка после серии неудачных попыток входа: 5 попыток → 5 минут.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Опции политики бронирования (часы до отмены, буфер между записями и т.д.).
builder.Services.Configure<BookingPolicyOptions>(
    builder.Configuration.GetSection(BookingPolicyOptions.SectionName));

// Заглушка отправки писем для разработки: вместо реального SMTP пишем в логи.
// При появлении SMTP-провайдера достаточно зарегистрировать другую реализацию.
builder.Services.AddSingleton<IEmailSender, DevEmailSender>();

builder.Services.AddRazorPages(options =>
{
    // Раздел «Админка» (филиалы, услуги, мастера, записи, клиенты, аналитика)
    // доступен владельцу сети и администратору филиала.
    options.Conventions.AuthorizeFolder("/Admin", "AdminAccess");
    // Управление пользователями и ролями — только владельцу.
    options.Conventions.AuthorizeFolder("/Admin/Users", "OwnerOnly");
    options.Conventions.AuthorizeFolder("/Admin/AuditLog", "OwnerOnly");
    // Кабинет мастера — владельцу, администратору и самому мастеру.
    options.Conventions.AuthorizeFolder("/Staff", "StaffAccess");
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnly", policy => policy.RequireRole("Owner"));
    options.AddPolicy("AdminAccess", policy => policy.RequireRole("Owner", "Admin"));
    options.AddPolicy("StaffAccess", policy => policy.RequireRole("Owner", "Admin", "Master"));
});
builder.Services.AddScoped<ISlotService>(sp =>
{
    var db = sp.GetRequiredService<ApplicationDbContext>();
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BookingPolicyOptions>>().Value;
    return new SlotService(db, opts.BufferMinutes);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DatabaseSeeder.SeedAsync(db);
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await IdentitySeeder.SeedAsync(db, roleManager, userManager);
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
