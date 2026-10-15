using System.Globalization;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Recommender;

/// <summary>
/// Content-based filtering for vehicle services.
/// <para>
/// A pure function of its input: no database, no clock, no randomness. The same input always
/// gives the same ranking, which is what makes it testable and what lets the documentation
/// describe exactly what it does. <c>recommender-dokumentacija.md</c> follows this file step by step.
/// </para>
/// <para>
/// Every service is a vector over the same features - its category, its tags weighted by how
/// rare they are (IDF), the engine types it is meant for. The vehicle gets a vector in the same
/// space, built from its own attributes and, once there is some, from the customer's history
/// with this vehicle. Cosine similarity of the two is the base of the score; named, separately
/// weighted factors (due by mileage or age, engine fit, rating, what is usually done on this
/// make and model, recently done) are added on top. Because every part of the score has a name,
/// every recommendation can say why it was made.
/// </para>
/// </summary>
public static class RecommenderEngine
{
    private const string CategoryPrefix = "c:";
    private const string TagPrefix = "t:";
    private const string EnginePrefix = "e:";
    private const string HighMileageTag = "high-mileage";
    private const string OlderVehicleTag = "older-vehicle";
    private const double DaysPerMonth = 30.44;
    private const double MinimumIdf = 0.1;
    private const double UnlistedEngineFit = 0.5;
    private const double BrandShareWeight = 0.6;
    private const double ModelShareWeight = 1.0;
    private const double RecentlyDoneRatio = 0.5;
    private const int MaxPerCategory = 2;
    private const int MaxReasons = 3;

    public static IReadOnlyList<Recommendation> Recommend(RecommenderInput input, RecommenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        var vehicle = input.Vehicle;
        var idf = InverseDocumentFrequency(input.Catalog);
        var items = input.Catalog.ToDictionary(s => s.ServiceId, s => ItemVector(s, idf));

        var (profile, historyCategories) = BuildProfile(input, items, idf, options);
        var knownEngineTypes = input.Catalog.SelectMany(s => s.EngineTypeIds).Distinct().Count();

        var scored = new List<(CatalogItem Service, double Score, double DueRatio, List<ScoreFactor> Factors)>();

        foreach (var service in input.Catalog)
        {
            // Hard filters. A petrol car is never offered diesel work, and a service already
            // booked for this vehicle is not suggested again.
            if (service.EngineTypeIds.Count > 0 && !service.EngineTypeIds.Contains(vehicle.EngineTypeId))
            {
                continue;
            }

            if (input.ServicesAlreadyBooked.Contains(service.ServiceId))
            {
                continue;
            }

            var factors = new List<ScoreFactor>();
            var similarity = Math.Max(0, profile.Dot(items[service.ServiceId]));

            factors.Add(historyCategories.Contains(service.CategoryId)
                ? new ScoreFactor(
                    FactorCodes.HistoryCategory,
                    $"već ste koristili usluge iz kategorije \"{service.CategoryName}\"",
                    similarity)
                : new ScoreFactor(
                    FactorCodes.Similarity,
                    "odgovara profilu vašeg vozila (motor, starost i kilometraža)",
                    similarity));

            input.LastDone.TryGetValue(service.ServiceId, out var lastDone);
            var (dueRatio, dueFactor) = DueFactor(service, vehicle, lastDone, input.NowUtc, options);

            if (dueFactor is not null)
            {
                factors.Add(dueFactor);
            }

            // Listing every engine type says no more than listing none, so only a service limited
            // to some engines earns the full weight and is named as a reason.
            factors.Add(service.EngineTypeIds.Count > 0 && service.EngineTypeIds.Count < knownEngineTypes
                ? new ScoreFactor(
                    FactorCodes.EngineMatch,
                    $"namijenjeno vozilima sa motorom: {vehicle.EngineName.ToLowerInvariant()}",
                    options.EngineWeight)
                : new ScoreFactor(
                    FactorCodes.EngineAny,
                    "odgovara svim tipovima motora",
                    options.EngineWeight * UnlistedEngineFit));

            if (FitsAge(service, vehicle.AgeYears))
            {
                factors.Add(new ScoreFactor(
                    FactorCodes.AgeFit,
                    $"preporučeno za vozila starosti {AgeRange(service)}",
                    options.AgeWeight));
            }

            if (service.ReviewCount > 0 && service.AverageRating is { } rating)
            {
                factors.Add(new ScoreFactor(
                    FactorCodes.Rating,
                    $"ocjena korisnika {rating.ToString("0.0", CultureInfo.InvariantCulture)}/5",
                    options.RatingWeight * (rating - 3) / 2));
            }

            if (input.Affinity.TryGetValue(service.ServiceId, out var affinity))
            {
                var byModel = ModelShareWeight * affinity.ModelShare;
                var byBrand = BrandShareWeight * affinity.BrandShare;

                if (byModel > 0 || byBrand > 0)
                {
                    factors.Add(new ScoreFactor(
                        FactorCodes.BrandModel,
                        byModel >= byBrand
                            ? $"često se radi na vozilima {vehicle.BrandName} {vehicle.ModelName}"
                            : $"često se radi na vozilima marke {vehicle.BrandName}",
                        options.BrandModelWeight * Math.Max(byModel, byBrand)));
                }
            }

            // Done recently and nowhere near due again: pushed down so the list is not taken up
            // by work the vehicle has just had.
            if (lastDone is not null && dueRatio is { } ratio && ratio < RecentlyDoneRatio)
            {
                factors.Add(new ScoreFactor(FactorCodes.RecentlyDone, "nedavno urađeno", -options.RecentPenalty));
            }

            scored.Add((service, factors.Sum(f => f.Contribution), dueRatio ?? 0, factors));
        }

        var perCategory = new Dictionary<int, int>();
        var results = new List<Recommendation>();

        // Ties broken by id so the order never depends on how the catalogue happened to load.
        foreach (var (service, score, dueRatio, factors) in scored.OrderByDescending(s => s.Score).ThenBy(s => s.Service.ServiceId))
        {
            if (results.Count == options.TopN)
            {
                break;
            }

            // At most two per category, so five oil-change variants cannot fill the list.
            var inCategory = perCategory.GetValueOrDefault(service.CategoryId);

            if (inCategory == MaxPerCategory)
            {
                continue;
            }

            perCategory[service.CategoryId] = inCategory + 1;

            results.Add(new Recommendation(
                service,
                Math.Round(score, 4),
                results.Count + 1,
                dueRatio >= options.UrgentThreshold ? RecommendationUrgency.Urgent : RecommendationUrgency.Planned,
                Explain(factors, options),
                factors));
        }

        return results;
    }

    /// <summary>
    /// A tag that most services carry says little about any one of them; a rare one says a lot.
    /// idf(t) = ln(N / (1 + df(t))), kept slightly above zero so no tag vanishes entirely.
    /// </summary>
    private static Dictionary<string, double> InverseDocumentFrequency(IReadOnlyList<CatalogItem> catalog)
    {
        var documentFrequency = catalog
            .SelectMany(s => s.Tags.Select(t => t.Slug).Distinct(StringComparer.Ordinal))
            .GroupBy(slug => slug, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return documentFrequency.ToDictionary(
            pair => pair.Key,
            pair => Math.Max(MinimumIdf, Math.Log(catalog.Count / (1.0 + pair.Value))),
            StringComparer.Ordinal);
    }

    private static SparseVector ItemVector(CatalogItem service, Dictionary<string, double> idf)
    {
        var vector = new SparseVector();
        vector.Add(CategoryPrefix + service.CategoryId, 1);

        foreach (var tag in service.Tags)
        {
            vector.Add(TagPrefix + tag.Slug, tag.Weight * idf.GetValueOrDefault(tag.Slug, MinimumIdf));
        }

        foreach (var engineTypeId in service.EngineTypeIds)
        {
            vector.Add(EnginePrefix + engineTypeId, 1);
        }

        return vector.Normalised();
    }

    /// <summary>
    /// The vehicle's profile: its own attributes, blended with the customer's history in
    /// proportion to how much history there is. With no signals at all the profile is the
    /// vehicle alone, which is why a brand-new customer still gets recommendations.
    /// </summary>
    private static (SparseVector Profile, IReadOnlySet<int> HistoryCategories) BuildProfile(
        RecommenderInput input,
        Dictionary<int, SparseVector> items,
        Dictionary<string, double> idf,
        RecommenderOptions options)
    {
        var vehicle = input.Vehicle;
        var attributes = new SparseVector();

        attributes.Add(EnginePrefix + vehicle.EngineTypeId, 1);
        AddTag(attributes, vehicle.EngineCode, idf);

        if (vehicle.MileageKm > options.HighMileageThresholdKm)
        {
            AddTag(attributes, HighMileageTag, idf);
        }

        if (vehicle.AgeYears > options.OldVehicleAgeYears)
        {
            AddTag(attributes, OlderVehicleTag, idf);
        }

        attributes = attributes.Normalised();

        var history = new SparseVector();
        var categoryWeight = new Dictionary<int, double>();
        var catalog = input.Catalog.ToDictionary(s => s.ServiceId);
        var evidence = 0.0;

        foreach (var signal in input.Signals)
        {
            if (!items.TryGetValue(signal.ServiceId, out var item))
            {
                continue;
            }

            // Old behaviour fades: a signal loses half its weight every half-life.
            var ageDays = Math.Max(0, (input.NowUtc - signal.OccurredAtUtc).TotalDays);
            var decay = Math.Pow(0.5, ageDays / options.SignalHalfLifeDays);
            var weight = signal.Weight * decay;

            history.AddScaled(item, weight);

            // The history is normalised, so decay alone would cancel out for a lone signal;
            // counting decayed evidence makes old history also weigh less against the vehicle.
            evidence += decay;

            var categoryId = catalog[signal.ServiceId].CategoryId;
            categoryWeight[categoryId] = categoryWeight.GetValueOrDefault(categoryId) + weight;
        }

        var alpha = Math.Min(1.0, evidence / options.SignalSaturationCount);
        var beta = options.HistoryBlendBeta;
        var historyPart = history.Normalised();

        var profile = new SparseVector();
        profile.AddScaled(attributes, 1 - alpha + (alpha * (1 - beta)));
        profile.AddScaled(historyPart, alpha * beta);

        var historyCategories = categoryWeight.Where(c => c.Value > 0).Select(c => c.Key).ToHashSet();

        return (profile.Normalised(), historyCategories);
    }

    /// <summary>
    /// How far the vehicle is through the recommended interval, by kilometres and by time,
    /// whichever is further along. 1 means exactly due. A service never done on this vehicle is
    /// measured from the vehicle's beginning: its whole mileage and its whole age.
    /// </summary>
    private static (double? Ratio, ScoreFactor? Factor) DueFactor(
        CatalogItem service,
        VehicleContext vehicle,
        LastDone? lastDone,
        DateTime now,
        RecommenderOptions options)
    {
        double? kmRatio = null;
        double? timeRatio = null;
        var kmSince = 0;
        var monthsSince = 0;

        if (service.IntervalKm is { } intervalKm)
        {
            int? since = lastDone is null ? vehicle.MileageKm
                : lastDone.MileageKm is { } atKm ? Math.Max(0, vehicle.MileageKm - atKm)
                : null;

            if (since is { } km)
            {
                kmSince = km;
                kmRatio = km / (double)intervalKm;
            }
        }

        if (service.IntervalMonths is { } intervalMonths)
        {
            monthsSince = lastDone is null
                ? vehicle.AgeYears * 12
                : (int)((now - lastDone.AtUtc).TotalDays / DaysPerMonth);
            timeRatio = monthsSince / (double)intervalMonths;
        }

        if (kmRatio is null && timeRatio is null)
        {
            return (null, null);
        }

        var byKm = (kmRatio ?? -1) >= (timeRatio ?? -1);
        var ratio = byKm ? kmRatio!.Value : timeRatio!.Value;
        var contribution = options.DueWeight * Math.Min(1, ratio);

        var label = byKm
            ? lastDone is null
                ? $"vozilo ima {Km(vehicle.MileageKm)} (interval: {Km(service.IntervalKm!.Value)})"
                : $"od zadnje usluge prešli ste {Km(kmSince)} (interval: {Km(service.IntervalKm!.Value)})"
            : lastDone is null
                ? $"vozilo je staro {Years(vehicle.AgeYears)} (preporuka na {Period(service.IntervalMonths!.Value)})"
                : $"od zadnje usluge je prošlo {Months(monthsSince)} (preporuka na {Period(service.IntervalMonths!.Value)})";

        return (ratio, new ScoreFactor(byKm ? FactorCodes.MileageDue : FactorCodes.TimeDue, label, contribution));
    }

    private static bool FitsAge(CatalogItem service, int ageYears) =>
        (service.MinAgeYears is not null || service.MaxAgeYears is not null)
        && ageYears >= (service.MinAgeYears ?? 0)
        && ageYears <= (service.MaxAgeYears ?? int.MaxValue);

    // The reasons a customer reads are the strongest positive parts of the score, in the
    // short lowercase form the approved mockup shows. A recommendation always has at least one.
    private static string Explain(IReadOnlyList<ScoreFactor> factors, RecommenderOptions options)
    {
        var candidates = factors
            .Where(f => f.Code != FactorCodes.EngineAny)
            .OrderByDescending(f => f.Contribution)
            .ToList();

        var reasons = candidates
            .Where(f => f.Contribution >= options.ExplanationMinContribution)
            .Take(MaxReasons)
            .Select(f => f.Label)
            .ToList();

        // Similarity is always among the candidates, so there is a reason even for a weak match.
        return reasons.Count > 0 ? string.Join("; ", reasons) : candidates[0].Label;
    }

    private static void AddTag(SparseVector vector, string slug, Dictionary<string, double> idf)
    {
        if (idf.TryGetValue(slug, out var weight))
        {
            vector.Add(TagPrefix + slug, weight);
        }
    }

    private static string AgeRange(CatalogItem service) =>
        (service.MinAgeYears, service.MaxAgeYears) switch
        {
            ({ } min, { } max) => $"{min}-{max} god.",
            ({ } min, null) => $"od {min} god.",
            (null, { } max) => $"do {max} god.",
            _ => string.Empty,
        };

    // Written the way the approved mockup writes it: \"45200 km\", no thousands separator.
    private static string Km(int km) => $"{km.ToString(CultureInfo.InvariantCulture)} km";

    private static string Period(int months) => months % 12 == 0 ? $"{months / 12} god." : $"{months} mj.";

    private static string Years(int years) => Plural(years, "godinu", "godine", "godina");

    private static string Months(int months) => Plural(months, "mjesec", "mjeseca", "mjeseci");

    // Bosnian picks the noun form by the last digits: 1 godinu, 2-4 godine, 5+ and 11-14 godina.
    private static string Plural(int n, string one, string few, string many) => (n % 10, n % 100) switch
    {
        (_, >= 11 and <= 14) => $"{n} {many}",
        (1, _) => $"{n} {one}",
        (>= 2 and <= 4, _) => $"{n} {few}",
        _ => $"{n} {many}",
    };

    /// <summary>A vector stored as only its non-zero entries, which is nearly all of them.</summary>
    private sealed class SparseVector
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);

        public void Add(string key, double value) => _values[key] = _values.GetValueOrDefault(key) + value;

        public void AddScaled(SparseVector other, double factor)
        {
            foreach (var (key, value) in other._values)
            {
                Add(key, value * factor);
            }
        }

        public double Dot(SparseVector other) =>
            _values.Sum(pair => pair.Value * other._values.GetValueOrDefault(pair.Key));

        public SparseVector Normalised()
        {
            var length = Math.Sqrt(_values.Values.Sum(v => v * v));
            var result = new SparseVector();

            if (length > 0)
            {
                foreach (var (key, value) in _values)
                {
                    result._values[key] = value / length;
                }
            }

            return result;
        }
    }
}
