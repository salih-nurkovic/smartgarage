namespace SmartGarage.Infrastructure.Persistence.Seeding;

// The ban on System.Random is suppressed for this whole file, and only this file.
//
// The ban itself is right and stays in force everywhere else: reset codes, tokens and
// anything an attacker could guess must come from RandomNumberGenerator. The build enforces
// that, and it is what the course instructions require in Appendix A.3.
//
// Seed data is the one place where the ban points at the wrong target. Nothing here is a
// security value, and the goal is the opposite of unpredictability: a fixed seed makes the
// demo database come out identical on every machine, so the student and whoever evaluates
// the work see exactly the same appointments, payments and reviews. RandomNumberGenerator
// cannot be seeded, so using it would make the demo database different on every run.
//
// Keeping System.Random in one small wrapper means it appears once, with the reasoning
// attached, instead of being suppressed in two dozen call sites.
#pragma warning disable RS0030

/// <summary>
/// A deterministic pseudo-random generator used only for building demo seed data.
/// <para>
/// Exposes the same method shapes as <c>System.Random</c>, so the seeders read the same way
/// they would with the real thing.
/// </para>
/// </summary>
internal sealed class SeedRandom
{
    private readonly Random _random;

    public SeedRandom(int seed) => _random = new Random(seed);

    /// <summary>A non-negative value below <paramref name="maxExclusive"/>.</summary>
    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    /// <summary>
    /// A value at or above <paramref name="minInclusive"/> and below <paramref name="maxExclusive"/>.
    /// </summary>
    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    /// <summary>Any non-negative value. Used for shuffling.</summary>
    public int Next() => _random.Next();
}

#pragma warning restore RS0030
