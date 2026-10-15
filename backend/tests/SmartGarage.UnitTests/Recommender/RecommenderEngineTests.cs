using FluentAssertions;
using SmartGarage.Application.Options;
using SmartGarage.Application.Recommender;
using SmartGarage.Domain.Enums;
using Xunit;

namespace SmartGarage.UnitTests.Recommender;

/// <summary>
/// The recommender on a small hand-made catalogue, where the right answer can be worked out
/// by hand. Each test pins down one property the documentation promises.
/// </summary>
public sealed class RecommenderEngineTests
{
    private const int Diesel = 1;
    private const int Petrol = 2;

    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static readonly RecommenderOptions Options = new()
    {
        TopN = 5,
        AlgorithmVersion = "test",
        SignalHalfLifeDays = 180,
        SignalSaturationCount = 8,
        HistoryBlendBeta = 0.7,
        HighMileageThresholdKm = 150_000,
        OldVehicleAgeYears = 10,
        EngineWeight = 0.20,
        DueWeight = 0.30,
        AgeWeight = 0.10,
        RatingWeight = 0.10,
        BrandModelWeight = 0.15,
        RecentPenalty = 0.50,
        ExplanationMinContribution = 0.05,
        UrgentThreshold = 1.0,
    };

    private static readonly CatalogItem OilChange = Item(1, "Zamjena ulja", 1, "Redovno održavanje", 15_000, 12, [], [new("oil", 1), new("preventive", 1)]);
    private static readonly CatalogItem DpfCleaning = Item(2, "Čišćenje DPF filtera", 3, "Motor", 60_000, null, [Diesel], [new("dpf", 1), new("diesel", 1)]);
    private static readonly CatalogItem SparkPlugs = Item(3, "Zamjena svjećica", 3, "Motor", 60_000, null, [Petrol], [new("petrol", 1)]);
    private static readonly CatalogItem TimingBelt = Item(4, "Zamjena zupčastog kaiša", 4, "Razvod", 90_000, 60, [], [new("timing-belt", 1), new("high-mileage", 1)]);
    private static readonly CatalogItem BrakeCheck = Item(5, "Pregled kočnica", 2, "Kočnice", null, 12, [], [new("brakes", 1), new("safety", 1)]);
    private static readonly CatalogItem AirCon = Item(6, "Punjenje klime", 7, "Klima", null, 24, [], [new("summer", 1)]);
    private static readonly CatalogItem Polishing = Item(7, "Poliranje laka", 8, "Karoserija", null, null, [], []);
    private static readonly CatalogItem Injectors = Item(8, "Zamjena dizni", 3, "Motor", 150_000, null, [Diesel], [new("diesel", 1), new("high-mileage", 1)]);

    private static readonly IReadOnlyList<CatalogItem> Catalog =
        [OilChange, DpfCleaning, SparkPlugs, TimingBelt, BrakeCheck, AirCon, Polishing, Injectors];

    [Fact]
    public void EngineType_IsAHardFilter_InBothDirections()
    {
        var forDiesel = Recommend(Vehicle(Diesel, "diesel"), topN: 20);
        var forPetrol = Recommend(Vehicle(Petrol, "petrol"), topN: 20);

        forDiesel.Select(r => r.Service.ServiceId).Should().NotContain(SparkPlugs.ServiceId);
        forPetrol.Select(r => r.Service.ServiceId).Should().NotContain([DpfCleaning.ServiceId, Injectors.ServiceId]);
    }

    [Fact]
    public void OnlyAnEngineSpecificService_GivesTheEngineAsAReason()
    {
        var results = Recommend(Vehicle(Diesel, "diesel"), topN: 20);

        results.Single(r => r.Service.ServiceId == DpfCleaning.ServiceId).Factors
            .Should().Contain(f => f.Code == FactorCodes.EngineMatch && f.Label.Contains("dizel"));
        results.Single(r => r.Service.ServiceId == BrakeCheck.ServiceId).Factors
            .Should().Contain(f => f.Code == FactorCodes.EngineAny)
            .And.NotContain(f => f.Code == FactorCodes.EngineMatch);
        results.Where(r => r.Service.EngineTypeIds.Count == 0)
            .Should().OnlyContain(r => !r.Explanation.Contains("tipovima motora") && !r.Explanation.Contains("namijenjeno"));
    }

    [Fact]
    public void ABrandNewCustomer_StillGetsAFullList_EachWithAReason()
    {
        var results = Recommend(Vehicle(Diesel, "diesel"));

        results.Should().HaveCount(5);
        results.Select(r => r.Rank).Should().Equal(1, 2, 3, 4, 5);
        results.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Explanation) && r.Factors.Count > 0);
        results.Select(r => r.Score).Should().BeInDescendingOrder();
    }

    [Fact]
    public void TheSameInput_AlwaysGivesTheSameRanking()
    {
        var first = Recommend(Vehicle(Diesel, "diesel", mileage: 182_000, age: 8));
        var second = Recommend(Vehicle(Diesel, "diesel", mileage: 182_000, age: 8));

        second.Select(r => (r.Service.ServiceId, r.Score)).Should().Equal(first.Select(r => (r.Service.ServiceId, r.Score)));
    }

    [Fact]
    public void AServiceFarPastItsInterval_IsUrgent_AndSaysWhyInTheMockupsWords()
    {
        var results = Recommend(Vehicle(Diesel, "diesel", mileage: 182_000, age: 8), topN: 20);
        var belt = results.Single(r => r.Service.ServiceId == TimingBelt.ServiceId);

        belt.Urgency.Should().Be(RecommendationUrgency.Urgent);
        belt.Explanation.Should().Contain("vozilo ima 182000 km (interval: 90000 km)");
        belt.Factors.Should().Contain(f => f.Code == FactorCodes.MileageDue && f.Contribution == Options.DueWeight);
    }

    [Fact]
    public void AgeBasedReason_ReadsLikeTheMockup_WhenTimeIsWhatMakesItDue()
    {
        var results = Recommend(Vehicle(Diesel, "diesel", mileage: 20_000, age: 8), topN: 20);
        var belt = results.Single(r => r.Service.ServiceId == TimingBelt.ServiceId);

        belt.Explanation.Should().Contain("vozilo je staro 8 godina (preporuka na 5 god.)");
        belt.Urgency.Should().Be(RecommendationUrgency.Urgent);
    }

    [Fact]
    public void AServiceDoneRecently_IsPushedDown_AndNotUrgent()
    {
        var vehicle = Vehicle(Diesel, "diesel", mileage: 182_000, age: 8);

        var before = Recommend(vehicle, topN: 20).Single(r => r.Service.ServiceId == TimingBelt.ServiceId);
        var after = Recommend(
                vehicle,
                topN: 20,
                lastDone: new Dictionary<int, LastDone> { [TimingBelt.ServiceId] = new(Now.AddMonths(-2), 180_000) })
            .Single(r => r.Service.ServiceId == TimingBelt.ServiceId);

        after.Score.Should().BeLessThan(before.Score - Options.RecentPenalty + 0.01);
        after.Urgency.Should().Be(RecommendationUrgency.Planned);
        after.Factors.Should().Contain(f => f.Code == FactorCodes.RecentlyDone && f.Contribution < 0);
        after.Explanation.Should().NotContain("nedavno", "a penalty is never presented as a reason");
    }

    [Fact]
    public void NewHistory_ChangesTheRanking_AndTheReasonGiven()
    {
        var vehicle = Vehicle(Petrol, "petrol", mileage: 30_000, age: 2);
        var without = Recommend(vehicle, topN: 20);

        var history = Enumerable.Range(1, 8)
            .Select(i => new SignalRecord(AirCon.ServiceId, 3.0, Now.AddDays(-i * 10)))
            .ToList();

        var with = Recommend(vehicle, topN: 20, signals: history);

        RankOf(with, AirCon).Should().BeLessThan(RankOf(without, AirCon));
        with.Single(r => r.Service.ServiceId == AirCon.ServiceId).Factors
            .Should().Contain(f => f.Code == FactorCodes.HistoryCategory && f.Label.Contains("Klima"));
    }

    [Fact]
    public void NegativeSignals_LowerAService_ThatPositiveOnesWouldRaise()
    {
        var vehicle = Vehicle(Petrol, "petrol", mileage: 30_000, age: 2);
        SignalRecord[] liked = [new(Polishing.ServiceId, 3.0, Now.AddDays(-5)), new(Polishing.ServiceId, 3.0, Now.AddDays(-6))];
        SignalRecord[] disliked = [new(Polishing.ServiceId, -1.5, Now.AddDays(-5)), new(Polishing.ServiceId, -1.0, Now.AddDays(-6))];

        var positive = ScoreOf(Recommend(vehicle, topN: 20, signals: liked), Polishing);
        var negative = ScoreOf(Recommend(vehicle, topN: 20, signals: disliked), Polishing);

        positive.Should().BeGreaterThan(negative);
    }

    [Fact]
    public void OldSignals_CountLessThanRecentOnes()
    {
        var vehicle = Vehicle(Petrol, "petrol", mileage: 30_000, age: 2);

        var recent = ScoreOf(Recommend(vehicle, topN: 20, signals: [new(AirCon.ServiceId, 3.0, Now.AddDays(-1))]), AirCon);
        var old = ScoreOf(Recommend(vehicle, topN: 20, signals: [new(AirCon.ServiceId, 3.0, Now.AddDays(-900))]), AirCon);

        recent.Should().BeGreaterThan(old);
    }

    [Fact]
    public void WhatIsUsuallyDoneOnThisModel_RaisesAService_AndIsNamedAsTheReason()
    {
        var vehicle = Vehicle(Petrol, "petrol", mileage: 30_000, age: 2);
        var affinity = new Dictionary<int, BrandModelAffinity> { [Polishing.ServiceId] = new(0.5, 1.0) };

        var with = Recommend(vehicle, topN: 20, affinity: affinity).Single(r => r.Service.ServiceId == Polishing.ServiceId);
        var without = Recommend(vehicle, topN: 20).Single(r => r.Service.ServiceId == Polishing.ServiceId);

        with.Score.Should().BeApproximately(without.Score + Options.BrandModelWeight, 0.0001);
        with.Factors.Should().Contain(f => f.Code == FactorCodes.BrandModel && f.Label.Contains("Audi A4"));
    }

    [Fact]
    public void AlreadyBookedServices_AreLeftOut_AndNoCategoryTakesMoreThanTwoPlaces()
    {
        var results = Recommend(
            Vehicle(Diesel, "diesel", mileage: 250_000, age: 12),
            topN: 20,
            booked: new HashSet<int> { OilChange.ServiceId });

        results.Should().NotContain(r => r.Service.ServiceId == OilChange.ServiceId);
        results.GroupBy(r => r.Service.CategoryId).Should().OnlyContain(g => g.Count() <= 2);
    }

    private static IReadOnlyList<Recommendation> Recommend(
        VehicleContext vehicle,
        int topN = 5,
        IReadOnlyList<SignalRecord>? signals = null,
        IReadOnlyDictionary<int, LastDone>? lastDone = null,
        IReadOnlyDictionary<int, BrandModelAffinity>? affinity = null,
        IReadOnlySet<int>? booked = null)
    {
        var options = new RecommenderOptions
        {
            TopN = topN,
            AlgorithmVersion = Options.AlgorithmVersion,
            SignalHalfLifeDays = Options.SignalHalfLifeDays,
            SignalSaturationCount = Options.SignalSaturationCount,
            HistoryBlendBeta = Options.HistoryBlendBeta,
            HighMileageThresholdKm = Options.HighMileageThresholdKm,
            OldVehicleAgeYears = Options.OldVehicleAgeYears,
            EngineWeight = Options.EngineWeight,
            DueWeight = Options.DueWeight,
            AgeWeight = Options.AgeWeight,
            RatingWeight = Options.RatingWeight,
            BrandModelWeight = Options.BrandModelWeight,
            RecentPenalty = Options.RecentPenalty,
            ExplanationMinContribution = Options.ExplanationMinContribution,
            UrgentThreshold = Options.UrgentThreshold,
        };

        return RecommenderEngine.Recommend(
            new RecommenderInput(
                vehicle,
                Catalog,
                signals ?? [],
                lastDone ?? new Dictionary<int, LastDone>(),
                affinity ?? new Dictionary<int, BrandModelAffinity>(),
                booked ?? new HashSet<int>(),
                Now),
            options);
    }

    private static VehicleContext Vehicle(int engineTypeId, string engineCode, int mileage = 60_000, int age = 4) =>
        new(1, 1, 1, "Audi", "A4", engineTypeId, engineCode, engineCode == "diesel" ? "Dizel" : "Benzin", age, mileage);

    private static CatalogItem Item(
        int id,
        string name,
        int categoryId,
        string categoryName,
        int? intervalKm,
        int? intervalMonths,
        IReadOnlyList<int> engineTypes,
        IReadOnlyList<CatalogTag> tags) =>
        new(id, name, categoryId, categoryName, 50m, 60, intervalKm, intervalMonths, null, null, engineTypes, tags, null, 0);

    private static int RankOf(IReadOnlyList<Recommendation> results, CatalogItem service) =>
        results.Single(r => r.Service.ServiceId == service.ServiceId).Rank;

    private static double ScoreOf(IReadOnlyList<Recommendation> results, CatalogItem service) =>
        results.Single(r => r.Service.ServiceId == service.ServiceId).Score;
}
