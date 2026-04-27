using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Infrastructure.Data;

/// <summary>
/// Заполняет БД базовым набором справочных данных (филиалы, услуги, мастера,
/// привязки и расписание мастеров на ближайшую неделю), если соответствующие
/// таблицы пусты. Используется только в среде разработки.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        if (await db.Branches.AnyAsync(ct))
        {
            return;
        }

        var branches = SeedBranches(db);
        var services = SeedServices(db);
        await db.SaveChangesAsync(ct);

        var masters = await SeedMastersAsync(db, ct);
        await db.SaveChangesAsync(ct);

        SeedMasterBranches(db, masters, branches);
        SeedMasterServices(db, masters, services);
        SeedWorkSchedules(db, masters, branches);
        await db.SaveChangesAsync(ct);
    }

    private static List<Branch> SeedBranches(ApplicationDbContext db)
    {
        var branches = new List<Branch>
        {
            new()
            {
                Name = "Тихий час — Центр",
                Address = "Краснодар, ул. Красная, 32",
                Phone = "+7 (861) 200-10-01",
                OpeningTime = new TimeOnly(9, 0),
                ClosingTime = new TimeOnly(21, 0)
            },
            new()
            {
                Name = "Тихий час — Юбилейный",
                Address = "Краснодар, ул. Тургенева, 188",
                Phone = "+7 (861) 200-10-02",
                OpeningTime = new TimeOnly(10, 0),
                ClosingTime = new TimeOnly(22, 0)
            },
            new()
            {
                Name = "Тихий час — Сочи Центральный",
                Address = "Сочи, ул. Навагинская, 7",
                Phone = "+7 (862) 200-10-03",
                OpeningTime = new TimeOnly(10, 0),
                ClosingTime = new TimeOnly(22, 0)
            }
        };

        db.Branches.AddRange(branches);
        return branches;
    }

    private static List<Service> SeedServices(ApplicationDbContext db)
    {
        // DisplayOrder задаёт порядок вывода в каталоге: сверху самые ходовые позиции.
        var services = new List<Service>
        {
            new()
            {
                Name = "Мужская стрижка",
                Description = "Стрижка ножницами и машинкой, мытьё головы, укладка.",
                DurationMinutes = 60,
                Price = 1500m,
                DisplayOrder = 10,
                Category = ServiceCategory.Haircut
            },
            new()
            {
                Name = "Стрижка машинкой",
                Description = "Стрижка одной длиной по всей голове.",
                DurationMinutes = 30,
                Price = 800m,
                DisplayOrder = 20,
                Category = ServiceCategory.Haircut
            },
            new()
            {
                Name = "Моделирование бороды",
                Description = "Стрижка и моделирование бороды опасной бритвой.",
                DurationMinutes = 45,
                Price = 1200m,
                DisplayOrder = 30,
                Category = ServiceCategory.Beard
            },
            new()
            {
                Name = "Королевское бритьё",
                Description = "Бритьё опасной бритвой с горячими полотенцами.",
                DurationMinutes = 60,
                Price = 1800m,
                DisplayOrder = 40,
                Category = ServiceCategory.Shave
            },
            new()
            {
                Name = "Камуфляж бороды",
                Description = "Тонирование бороды для маскировки седины.",
                DurationMinutes = 45,
                Price = 1500m,
                DisplayOrder = 50,
                Category = ServiceCategory.Coloring
            },
            new()
            {
                Name = "Детская стрижка",
                Description = "Стрижка для клиентов до 12 лет.",
                DurationMinutes = 45,
                Price = 1000m,
                DisplayOrder = 60,
                Category = ServiceCategory.Kids
            }
        };

        db.Services.AddRange(services);
        return services;
    }

    private static async Task<List<Master>> SeedMastersAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var personas = new List<Persona>
        {
            new() { LastName = "Соколов",  FirstName = "Артём", MiddleName = "Игоревич",   Phone = "+79180000001", Gender = Gender.Male, BirthDate = new DateOnly(1992, 4, 12) },
            new() { LastName = "Ковалёв",  FirstName = "Денис", MiddleName = "Сергеевич",  Phone = "+79180000002", Gender = Gender.Male, BirthDate = new DateOnly(1988, 9, 5)  },
            new() { LastName = "Лазарев",  FirstName = "Михаил",MiddleName = "Александрович", Phone = "+79180000003", Gender = Gender.Male, BirthDate = new DateOnly(1995, 1, 23) },
            new() { LastName = "Романов",  FirstName = "Илья",  MiddleName = "Дмитриевич", Phone = "+79180000004", Gender = Gender.Male, BirthDate = new DateOnly(1990, 11, 30) },
            new() { LastName = "Орлов",    FirstName = "Тимур", MiddleName = "Русланович", Phone = "+79180000005", Gender = Gender.Male, BirthDate = new DateOnly(1997, 7, 14) }
        };

        db.Personas.AddRange(personas);
        await db.SaveChangesAsync(ct);

        var masters = new List<Master>
        {
            new() { PersonaId = personas[0].PersonaId, Position = "Топ-мастер",      HireDate = new DateOnly(2022, 3, 1),  Bio = "Специализация — fade и классические мужские стрижки.", IsActive = true },
            new() { PersonaId = personas[1].PersonaId, Position = "Барбер",          HireDate = new DateOnly(2022, 8, 15), Bio = "Специализация — работа с бородой, опасное бритьё.", IsActive = true },
            new() { PersonaId = personas[2].PersonaId, Position = "Барбер",          HireDate = new DateOnly(2023, 1, 10), Bio = "Молодёжные стрижки, тренды.", IsActive = true },
            new() { PersonaId = personas[3].PersonaId, Position = "Старший барбер",  HireDate = new DateOnly(2021, 5, 1),  Bio = "Универсал, более 8 лет в профессии.", IsActive = true },
            new() { PersonaId = personas[4].PersonaId, Position = "Барбер-стажёр",   HireDate = new DateOnly(2024, 9, 1),  Bio = "Стажёр, под наставничеством.", IsActive = true }
        };

        db.Masters.AddRange(masters);
        return masters;
    }

    private static void SeedMasterBranches(ApplicationDbContext db, List<Master> masters, List<Branch> branches)
    {
        // Каждый мастер закреплён ровно за одним филиалом сети — так его
        // расписание и записи однозначно относятся к одному адресу.
        // Соколов → Центр
        // Ковалёв → Центр
        // Лазарев → Юбилейный
        // Романов → Сочи
        // Орлов   → Сочи
        var links = new (int MasterIndex, int BranchIndex)[]
        {
            (0, 0),
            (1, 0),
            (2, 1),
            (3, 2),
            (4, 2)
        };

        foreach (var (m, b) in links)
        {
            db.MasterBranches.Add(new MasterBranch
            {
                MasterId = masters[m].MasterId,
                BranchId = branches[b].BranchId
            });
        }
    }

    private static void SeedMasterServices(ApplicationDbContext db, List<Master> masters, List<Service> services)
    {
        // Все мастера умеют делать первые две услуги (стрижки),
        // остальные — выборочно.
        for (var i = 0; i < masters.Count; i++)
        {
            db.MasterServices.Add(new MasterService { MasterId = masters[i].MasterId, ServiceId = services[0].ServiceId }); // Мужская
            db.MasterServices.Add(new MasterService { MasterId = masters[i].MasterId, ServiceId = services[1].ServiceId }); // Машинкой
        }

        // Борода / бритьё — для барберов с этим профилем
        foreach (var m in new[] { masters[0], masters[1], masters[3] })
        {
            db.MasterServices.Add(new MasterService { MasterId = m.MasterId, ServiceId = services[2].ServiceId });
            db.MasterServices.Add(new MasterService { MasterId = m.MasterId, ServiceId = services[3].ServiceId });
            db.MasterServices.Add(new MasterService { MasterId = m.MasterId, ServiceId = services[4].ServiceId });
        }

        // Детская — у универсалов
        foreach (var m in new[] { masters[0], masters[3] })
        {
            db.MasterServices.Add(new MasterService { MasterId = m.MasterId, ServiceId = services[5].ServiceId });
        }
    }

    private static void SeedWorkSchedules(ApplicationDbContext db, List<Master> masters, List<Branch> branches)
    {
        // На ближайшие 7 дней — рабочие смены 10:00–20:00 (с обедом 14:00–15:00).
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var masterToBranch = new Dictionary<int, int>
        {
            { masters[0].MasterId, branches[0].BranchId },
            { masters[1].MasterId, branches[0].BranchId },
            { masters[2].MasterId, branches[1].BranchId },
            { masters[3].MasterId, branches[2].BranchId },
            { masters[4].MasterId, branches[2].BranchId }
        };

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = today.AddDays(dayOffset);

            foreach (var (masterId, branchId) in masterToBranch)
            {
                // Утренняя смена 10:00–14:00
                db.WorkSchedules.Add(new WorkSchedule
                {
                    MasterId = masterId,
                    BranchId = branchId,
                    WorkDate = date,
                    StartTime = new TimeOnly(10, 0),
                    EndTime = new TimeOnly(14, 0),
                    ScheduleType = ScheduleType.Work
                });

                // Обед 14:00–15:00
                db.WorkSchedules.Add(new WorkSchedule
                {
                    MasterId = masterId,
                    BranchId = branchId,
                    WorkDate = date,
                    StartTime = new TimeOnly(14, 0),
                    EndTime = new TimeOnly(15, 0),
                    ScheduleType = ScheduleType.Lunch
                });

                // Вечерняя смена 15:00–20:00
                db.WorkSchedules.Add(new WorkSchedule
                {
                    MasterId = masterId,
                    BranchId = branchId,
                    WorkDate = date,
                    StartTime = new TimeOnly(15, 0),
                    EndTime = new TimeOnly(20, 0),
                    ScheduleType = ScheduleType.Work
                });
            }
        }
    }
}
