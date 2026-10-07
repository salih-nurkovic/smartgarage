using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the service catalogue together with the tag and engine-type links that the recommender
/// scores against.
/// <para>
/// The interval and age fields are not decoration. They are what lets a recommendation say
/// "due around 180 000 km" or "vozilo je staro 8 godina (preporuka na 5 god.)" instead of
/// producing a vague suggestion with no reason attached.
/// </para>
/// </summary>
public sealed class CatalogueSeeder
{
    private readonly SmartGarageDbContext _db;
    private readonly ILogger<CatalogueSeeder> _logger;

    public CatalogueSeeder(SmartGarageDbContext db, ILogger<CatalogueSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>One catalogue row plus everything the recommender needs to reason about it.</summary>
    private sealed record ServiceSeed(
        string Name,
        string Description,
        int CategoryId,
        decimal Price,
        int DurationMinutes,
        int? MileageIntervalKm,
        int? MonthsInterval,
        int? MinAgeYears,
        int? MaxAgeYears,
        int[] TagIds,
        int[] EngineTypeIds);

    // Engine type ids from ReferenceDataSeeder: 1 diesel, 2 petrol, 3 hybrid, 4 electric, 5 LPG.
    private static readonly int[] AllCombustion = [1, 2, 3, 5];
    private static readonly int[] AllEngines = [1, 2, 3, 4, 5];
    private static readonly int[] DieselOnly = [1];
    private static readonly int[] PetrolAndLpg = [2, 5];

    public async Task SeedAsync(DateTime nowUtc, CancellationToken ct)
    {
        if (await _db.Services.AnyAsync(ct))
        {
            return;
        }

        foreach (var seed in BuildCatalogue())
        {
            var service = new Service
            {
                Name = seed.Name,
                Description = seed.Description,
                CategoryId = seed.CategoryId,
                Price = seed.Price,
                EstimatedDurationMinutes = seed.DurationMinutes,
                RecommendedMileageIntervalKm = seed.MileageIntervalKm,
                RecommendedMonthsInterval = seed.MonthsInterval,
                MinVehicleAgeYears = seed.MinAgeYears,
                MaxVehicleAgeYears = seed.MaxAgeYears,
                ImagePath = $"/uploads/services/{Slugify(seed.Name)}.jpg",
                IsActive = true,
                CreatedAtUtc = nowUtc,
            };

            _db.Services.Add(service);
            await _db.SaveChangesAsync(ct);

            foreach (var tagId in seed.TagIds)
            {
                _db.ServiceTagLinks.Add(new ServiceTagLink
                {
                    ServiceId = service.Id,
                    ServiceTagId = tagId,

                    // The first tag listed is the defining one, the rest are supporting. This
                    // asymmetry is what stops every service looking equally like every other
                    // once the vectors are normalised.
                    Weight = tagId == seed.TagIds[0] ? 1.0 : 0.6,
                });
            }

            foreach (var engineTypeId in seed.EngineTypeIds)
            {
                _db.ServiceEngineTypeLinks.Add(new ServiceEngineTypeLink
                {
                    ServiceId = service.Id,
                    EngineTypeId = engineTypeId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var count = await _db.Services.CountAsync(ct);
        _logger.LogInformation("Seeded {Count} catalogue services with tags and engine links.", count);
    }

    private static List<ServiceSeed> BuildCatalogue() =>
    [
        // ---------- 1. Redovno održavanje ----------
        new("Zamjena ulja i filtera",
            "Kompletan servis zamjene motornog ulja i svih potrebnih filtera.",
            1, 75.00m, 45, 15_000, 12, null, null, [6, 7, 5], AllCombustion),

        new("Zamjena filtera zraka",
            "Zamjena filtera zraka radi očuvanja potrošnje i snage motora.",
            1, 25.00m, 20, 30_000, 24, null, null, [7, 5], AllCombustion),

        new("Zamjena filtera goriva",
            "Zamjena filtera goriva, preventiva za sistem ubrizgavanja.",
            1, 40.00m, 40, 40_000, 36, null, null, [7, 5], AllCombustion),

        new("Zamjena filtera kabine",
            "Zamjena polenskog filtera i dezinfekcija ventilacionog sistema.",
            1, 30.00m, 25, 20_000, 12, null, null, [7, 9], AllEngines),

        new("Mali servis",
            "Ulje, filter ulja i vizuelni pregled osnovnih sistema vozila.",
            1, 95.00m, 60, 15_000, 12, null, null, [5, 6, 7], AllCombustion),

        new("Veliki servis",
            "Sve iz malog servisa uz zamjenu svih filtera i detaljan pregled.",
            1, 180.00m, 150, 60_000, 48, null, null, [5, 7, 9], AllCombustion),

        new("Redovan godišnji pregled",
            "Sveobuhvatna provjera vozila prije tehničkog pregleda.",
            1, 55.00m, 60, null, 12, null, null, [9, 20, 8], AllEngines),

        // ---------- 2. Kočnice ----------
        new("Pregled kočnica",
            "Detaljan pregled kočionog sistema, diskova i pločica.",
            2, 40.00m, 45, 20_000, 12, null, null, [15, 8], AllEngines),

        new("Zamjena kočionih pločica",
            "Zamjena prednjih ili zadnjih kočionih pločica uz provjeru diskova.",
            2, 85.00m, 90, 40_000, null, null, null, [15, 8], AllEngines),

        new("Zamjena kočionih diskova",
            "Zamjena diskova i pločica, uključujući provjeru čeljusti.",
            2, 190.00m, 120, 70_000, null, null, null, [15, 8], AllEngines),

        new("Zamjena kočione tekućine",
            "Ispuštanje i zamjena kočione tekućine uz odzračivanje sistema.",
            2, 60.00m, 60, null, 24, null, null, [15, 8, 5], AllEngines),

        new("Servis ručne kočnice",
            "Podešavanje i popravka sistema parkirne kočnice.",
            2, 50.00m, 60, null, null, 5, null, [15, 8], AllEngines),

        // ---------- 3. Motor ----------
        new("Zamjena zupčastog kaiša",
            "Zamjena zupčastog kaiša sa zatezačem i pumpom za vodu.",
            3, 350.00m, 240, 90_000, 60, 4, null, [13, 5], AllCombustion),

        new("Čišćenje DPF filtera",
            "Regeneracija i čišćenje filtera čvrstih čestica kod dizel motora.",
            3, 160.00m, 120, 120_000, null, 4, null, [12, 1], DieselOnly),

        new("Zamjena svjećica",
            "Zamjena svjećica za paljenje kod benzinskih i plinskih motora.",
            3, 65.00m, 60, 40_000, 36, null, null, [2, 5], PetrolAndLpg),

        new("Zamjena dizni",
            "Pregled i zamjena dizni kod dizel motora.",
            3, 420.00m, 300, 150_000, null, 6, null, [1, 3], DieselOnly),

        new("Zamjena vodene pumpe",
            "Zamjena pumpe za vodu i provjera rashladnog sistema.",
            3, 220.00m, 180, 100_000, null, 5, null, [18, 3], AllCombustion),

        new("Zamjena rashladne tekućine",
            "Ispuštanje, ispiranje i punjenje rashladnog sistema.",
            3, 70.00m, 75, 60_000, 36, null, null, [18, 5], AllCombustion),

        new("Zamjena ulja u mjenjaču",
            "Zamjena ulja u ručnom ili automatskom mjenjaču.",
            3, 140.00m, 120, 80_000, 48, null, null, [19, 5], AllCombustion),

        new("Zamjena kvačila",
            "Zamjena kompletnog seta kvačila sa zamajcem po potrebi.",
            3, 560.00m, 420, 150_000, null, 5, null, [19, 3], AllCombustion),

        // ---------- 4. Gume ----------
        new("Zamjena guma",
            "Montaža i balansiranje kompleta od četiri gume.",
            4, 55.00m, 60, null, 6, null, null, [10, 8], AllEngines),

        new("Balansiranje točkova",
            "Balansiranje svih točkova radi mirnije vožnje.",
            4, 30.00m, 45, 20_000, 12, null, null, [8], AllEngines),

        new("Geometrija trapa",
            "Podešavanje geometrije prednje i zadnje osovine.",
            4, 65.00m, 90, 30_000, 24, null, null, [16, 8], AllEngines),

        new("Popravka gume",
            "Krpljenje probušene gume i provjera pritiska.",
            4, 20.00m, 30, null, null, null, null, [8], AllEngines),

        new("Sezonska zamjena guma",
            "Zamjena ljetnih i zimskih guma uz čuvanje kompleta.",
            4, 45.00m, 50, null, 6, null, null, [10, 11], AllEngines),

        // ---------- 5. Dijagnostika ----------
        new("Kompjuterska dijagnostika",
            "Očitavanje i tumačenje grešaka iz svih upravljačkih jedinica.",
            5, 45.00m, 60, null, 12, null, null, [17, 5], AllEngines),

        new("Dijagnostika motora",
            "Detaljna dijagnostika rada motora uz mjerenje parametara.",
            5, 90.00m, 120, null, null, 3, null, [17, 3], AllCombustion),

        new("Provjera emisije izduvnih gasova",
            "Mjerenje emisije prije tehničkog pregleda.",
            5, 35.00m, 40, null, 12, null, null, [20, 9], AllCombustion),

        // ---------- 6. Elektrika ----------
        new("Zamjena akumulatora",
            "Zamjena akumulatora uz provjeru sistema punjenja.",
            6, 120.00m, 45, null, 48, 3, null, [14, 10], AllEngines),

        new("Provjera alternatora",
            "Ispitivanje alternatora i regulatora napona.",
            6, 50.00m, 60, null, null, 5, null, [14, 17], AllCombustion),

        new("Popravka osvjetljenja",
            "Zamjena sijalica i popravka svjetlosne instalacije.",
            6, 40.00m, 45, null, null, null, null, [17, 8], AllEngines),

        new("Servis startera",
            "Pregled, čišćenje ili zamjena elektropokretača.",
            6, 150.00m, 120, null, null, 6, null, [14, 17], AllCombustion),

        // ---------- 7. Klima ----------
        new("Punjenje klima uređaja",
            "Punjenje rashladnog sredstva i provjera curenja.",
            7, 70.00m, 60, null, 24, null, null, [11, 18], AllEngines),

        new("Dezinfekcija klima sistema",
            "Ozonsko čišćenje i dezinfekcija ventilacionog sistema.",
            7, 45.00m, 45, null, 12, null, null, [11, 7], AllEngines),

        new("Provjera klima uređaja",
            "Dijagnostika rada klime i mjerenje pritiska u sistemu.",
            7, 40.00m, 45, null, 24, 2, null, [11, 17], AllEngines),

        // ---------- 8. Karoserija ----------
        new("Poliranje laka",
            "Mašinsko poliranje i zaštita laka vozila.",
            8, 130.00m, 180, null, 24, null, null, [11], AllEngines),

        new("Popravka ogrebotina",
            "Lakiranje i popravka manjih oštećenja na karoseriji.",
            8, 180.00m, 240, null, null, null, null, [], AllEngines),

        new("Zaštita podvozja",
            "Nanošenje zaštitnog sloja protiv korozije.",
            8, 160.00m, 180, null, 36, 5, null, [10, 4], AllEngines),
    ];

    /// <summary>
    /// Turns a service name into a file-safe slug for its seeded image path. Bosnian diacritics
    /// are folded to their ASCII equivalents so the path works on every filesystem.
    /// </summary>
    private static string Slugify(string value)
    {
        var folded = value
            .Replace("č", "c", StringComparison.Ordinal)
            .Replace("ć", "c", StringComparison.Ordinal)
            .Replace("ž", "z", StringComparison.Ordinal)
            .Replace("š", "s", StringComparison.Ordinal)
            .Replace("đ", "d", StringComparison.Ordinal)
            .ToLowerInvariant();

        var chars = folded
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
