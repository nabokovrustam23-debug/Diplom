using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages(options =>
{
    // Раздел «Админка» (филиалы, услуги, мастера, записи, клиенты, аналитика)
    // доступен владельцу сети и администратору филиала.
    options.Conventions.AuthorizeFolder("/Admin", "AdminAccess");
    // Управление пользователями и ролями — только владельцу.
    options.Conventions.AuthorizeFolder("/Admin/Users", "OwnerOnly");
    // Кабинет мастера — владельцу, администратору и самому мастеру.
    options.Conventions.AuthorizeFolder("/Staff", "StaffAccess");
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnly", policy => policy.RequireRole("Owner"));
    options.AddPolicy("AdminAccess", policy => policy.RequireRole("Owner", "Admin"));
    options.AddPolicy("StaffAccess", policy => policy.RequireRole("Owner", "Admin", "Master"));
});
builder.Services.AddScoped<ISlotService, SlotService>();

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
