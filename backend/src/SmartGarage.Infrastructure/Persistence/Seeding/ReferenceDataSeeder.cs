using Microsoft.EntityFrameworkCore;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Infrastructure.Persistence.Seeding;

/// <summary>
/// Reference data seeded through <c>HasData</c>, so it becomes part of the migration itself.
/// <para>
/// This tier is deterministic and has fixed primary keys. It belongs in the schema rather than
/// in a runtime seeder because the rest of the data depends on it: a service points at a
/// category, a vehicle points at a model. If these ids drifted between machines, every foreign
/// key in the demo data would drift with them.
/// </para>
/// <para>
/// No user is ever seeded here. The instructions warn that <c>HasData</c> and a runtime seeder
/// must not use different password hash formats, and the simplest way to guarantee that is for
/// exactly one of them to create users at all.
/// </para>
/// </summary>
public static class ReferenceDataSeeder
{
    public static void Seed(ModelBuilder builder)
    {
        SeedEngineTypes(builder);
        SeedVehicleBrands(builder);
        SeedVehicleModels(builder);
        SeedServiceCategories(builder);
        SeedServiceTags(builder);
    }

    private static void SeedEngineTypes(ModelBuilder builder) =>
        builder.Entity<EngineType>().HasData(
            new EngineType { Id = 1, Name = "Dizel", Code = "diesel", IsActive = true },
            new EngineType { Id = 2, Name = "Benzin", Code = "petrol", IsActive = true },
            new EngineType { Id = 3, Name = "Hibrid", Code = "hybrid", IsActive = true },
            new EngineType { Id = 4, Name = "Električni", Code = "electric", IsActive = true },
            new EngineType { Id = 5, Name = "Plin (LPG)", Code = "lpg", IsActive = true });

    private static void SeedVehicleBrands(ModelBuilder builder) =>
        builder.Entity<VehicleBrand>().HasData(
            new VehicleBrand { Id = 1, Name = "Volkswagen", IsActive = true },
            new VehicleBrand { Id = 2, Name = "Audi", IsActive = true },
            new VehicleBrand { Id = 3, Name = "BMW", IsActive = true },
            new VehicleBrand { Id = 4, Name = "Mercedes-Benz", IsActive = true },
            new VehicleBrand { Id = 5, Name = "Škoda", IsActive = true },
            new VehicleBrand { Id = 6, Name = "Toyota", IsActive = true },
            new VehicleBrand { Id = 7, Name = "Renault", IsActive = true },
            new VehicleBrand { Id = 8, Name = "Opel", IsActive = true },
            new VehicleBrand { Id = 9, Name = "Ford", IsActive = true },
            new VehicleBrand { Id = 10, Name = "Peugeot", IsActive = true });

    private static void SeedVehicleModels(ModelBuilder builder) =>
        builder.Entity<VehicleModel>().HasData(
            // Volkswagen
            new VehicleModel { Id = 1, BrandId = 1, Name = "Golf", IsActive = true },
            new VehicleModel { Id = 2, BrandId = 1, Name = "Passat", IsActive = true },
            new VehicleModel { Id = 3, BrandId = 1, Name = "Polo", IsActive = true },
            new VehicleModel { Id = 4, BrandId = 1, Name = "Tiguan", IsActive = true },
            // Audi
            new VehicleModel { Id = 5, BrandId = 2, Name = "A3", IsActive = true },
            new VehicleModel { Id = 6, BrandId = 2, Name = "A4", IsActive = true },
            new VehicleModel { Id = 7, BrandId = 2, Name = "A6", IsActive = true },
            new VehicleModel { Id = 8, BrandId = 2, Name = "Q5", IsActive = true },
            // BMW
            new VehicleModel { Id = 9, BrandId = 3, Name = "Serija 3", IsActive = true },
            new VehicleModel { Id = 10, BrandId = 3, Name = "Serija 5", IsActive = true },
            new VehicleModel { Id = 11, BrandId = 3, Name = "X3", IsActive = true },
            new VehicleModel { Id = 12, BrandId = 3, Name = "M3", IsActive = true },
            // Mercedes-Benz
            new VehicleModel { Id = 13, BrandId = 4, Name = "A-klasa", IsActive = true },
            new VehicleModel { Id = 14, BrandId = 4, Name = "C-klasa", IsActive = true },
            new VehicleModel { Id = 15, BrandId = 4, Name = "E-klasa", IsActive = true },
            new VehicleModel { Id = 16, BrandId = 4, Name = "GLC", IsActive = true },
            // Škoda
            new VehicleModel { Id = 17, BrandId = 5, Name = "Octavia", IsActive = true },
            new VehicleModel { Id = 18, BrandId = 5, Name = "Fabia", IsActive = true },
            new VehicleModel { Id = 19, BrandId = 5, Name = "Superb", IsActive = true },
            new VehicleModel { Id = 20, BrandId = 5, Name = "Kodiaq", IsActive = true },
            // Toyota
            new VehicleModel { Id = 21, BrandId = 6, Name = "Corolla", IsActive = true },
            new VehicleModel { Id = 22, BrandId = 6, Name = "Yaris", IsActive = true },
            new VehicleModel { Id = 23, BrandId = 6, Name = "RAV4", IsActive = true },
            new VehicleModel { Id = 24, BrandId = 6, Name = "Avensis", IsActive = true },
            // Renault
            new VehicleModel { Id = 25, BrandId = 7, Name = "Clio", IsActive = true },
            new VehicleModel { Id = 26, BrandId = 7, Name = "Megane", IsActive = true },
            new VehicleModel { Id = 27, BrandId = 7, Name = "Captur", IsActive = true },
            new VehicleModel { Id = 28, BrandId = 7, Name = "Scenic", IsActive = true },
            // Opel
            new VehicleModel { Id = 29, BrandId = 8, Name = "Astra", IsActive = true },
            new VehicleModel { Id = 30, BrandId = 8, Name = "Corsa", IsActive = true },
            new VehicleModel { Id = 31, BrandId = 8, Name = "Insignia", IsActive = true },
            new VehicleModel { Id = 32, BrandId = 8, Name = "Zafira", IsActive = true },
            // Ford
            new VehicleModel { Id = 33, BrandId = 9, Name = "Focus", IsActive = true },
            new VehicleModel { Id = 34, BrandId = 9, Name = "Fiesta", IsActive = true },
            new VehicleModel { Id = 35, BrandId = 9, Name = "Mondeo", IsActive = true },
            new VehicleModel { Id = 36, BrandId = 9, Name = "Kuga", IsActive = true },
            // Peugeot
            new VehicleModel { Id = 37, BrandId = 10, Name = "208", IsActive = true },
            new VehicleModel { Id = 38, BrandId = 10, Name = "308", IsActive = true },
            new VehicleModel { Id = 39, BrandId = 10, Name = "3008", IsActive = true },
            new VehicleModel { Id = 40, BrandId = 10, Name = "508", IsActive = true });

    private static void SeedServiceCategories(ModelBuilder builder) =>
        builder.Entity<ServiceCategory>().HasData(
            new ServiceCategory
            {
                Id = 1,
                Name = "Redovno održavanje",
                Description = "Planirani servisi koji produžavaju vijek trajanja vozila.",
                DisplayOrder = 1,
                IsActive = true,
                IsDeleted = false,
            },
            new ServiceCategory
            {
                Id = 2,
                Name = "Kočnice",
                Description = "Pregled i zamjena kočionog sistema.",
                DisplayOrder = 2,
                IsActive = true,
                IsDeleted = false,
            },
            new ServiceCategory
            {
                Id = 3,
                Name = "Motor",
                Description = "Mehanički radovi na motoru i pogonskom sklopu.",
                DisplayOrder = 3,
                IsActive = true,
                IsDeleted = false,
            },
            new ServiceCategory
            {
                Id = 4,
                Name = "Gume",
                Description = "Montaža, balansiranje i zamjena guma.",
                DisplayOrder = 4,
                IsActive = true,
                IsDeleted = false,
            },
            new ServiceCategory
            {
                Id = 5,
                Name = "Dijagnostika",
                Description = "Kompjuterska dijagnostika i otklanjanje grešaka.",
                DisplayOrder = 5,
                IsActive = true,
                IsDeleted = false,
            },
            new ServiceCategory
            {
                Id = 6,
                Name = "Elektrika",
                Description = "Akumulator, alternator, osvjetljenje i instalacije.",
                DisplayOrder = 6,
                IsActive = true,
                IsDeleted = false,
            },
            new ServiceCategory
            {
                Id = 7,
                Name = "Klima",
                Description = "Punjenje, dezinfekcija i popravka klima uređaja.",
                DisplayOrder = 7,
                IsActive = true,
                IsDeleted = false,
            },
            new ServiceCategory
            {
                Id = 8,
                Name = "Karoserija",
                Description = "Limarski i lakirerski radovi.",
                DisplayOrder = 8,
                IsActive = true,
                IsDeleted = false,
            });

    /// <summary>
    /// Tags are the vocabulary of the recommender's feature vector. They are deliberately a
    /// mix of broad and narrow: IDF weighting makes a tag carried by most services contribute
    /// almost nothing, while a rare one such as "dpf" or "zupcasti-kais" dominates a match.
    /// </summary>
    private static void SeedServiceTags(ModelBuilder builder) =>
        builder.Entity<ServiceTag>().HasData(
            new ServiceTag { Id = 1, Name = "Dizel", Slug = "diesel", IsActive = true },
            new ServiceTag { Id = 2, Name = "Benzin", Slug = "petrol", IsActive = true },
            new ServiceTag { Id = 3, Name = "Velika kilometraža", Slug = "high-mileage", IsActive = true },
            new ServiceTag { Id = 4, Name = "Starije vozilo", Slug = "older-vehicle", IsActive = true },
            new ServiceTag { Id = 5, Name = "Preventiva", Slug = "preventive", IsActive = true },
            new ServiceTag { Id = 6, Name = "Ulje", Slug = "oil", IsActive = true },
            new ServiceTag { Id = 7, Name = "Filter", Slug = "filter", IsActive = true },
            new ServiceTag { Id = 8, Name = "Sigurnost", Slug = "safety", IsActive = true },
            new ServiceTag { Id = 9, Name = "Godišnje", Slug = "annual", IsActive = true },
            new ServiceTag { Id = 10, Name = "Zima", Slug = "winter", IsActive = true },
            new ServiceTag { Id = 11, Name = "Ljeto", Slug = "summer", IsActive = true },
            new ServiceTag { Id = 12, Name = "DPF", Slug = "dpf", IsActive = true },
            new ServiceTag { Id = 13, Name = "Zupčasti kaiš", Slug = "timing-belt", IsActive = true },
            new ServiceTag { Id = 14, Name = "Akumulator", Slug = "battery", IsActive = true },
            new ServiceTag { Id = 15, Name = "Kočnice", Slug = "brakes", IsActive = true },
            new ServiceTag { Id = 16, Name = "Ovjes", Slug = "suspension", IsActive = true },
            new ServiceTag { Id = 17, Name = "Elektronika", Slug = "electronics", IsActive = true },
            new ServiceTag { Id = 18, Name = "Hlađenje", Slug = "cooling", IsActive = true },
            new ServiceTag { Id = 19, Name = "Mjenjač", Slug = "transmission", IsActive = true },
            new ServiceTag { Id = 20, Name = "Tehnički pregled", Slug = "inspection", IsActive = true });
}
