using FluentAssertions;
using SmartGarage.Domain.Constants;
using Xunit;

namespace SmartGarage.UnitTests.Constants;

/// <summary>
/// The course instructions treat an unbounded list endpoint as a defect: page size must have
/// a maximum limit that the server enforces regardless of what the client asks for. These
/// tests pin that behaviour down so a later refactor cannot quietly remove the clamp.
/// </summary>
public sealed class PagingLimitsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    public void ClampPageSize_KeepsValuesInsideTheAllowedRange(int requested, int expected) =>
        PagingLimits.ClampPageSize(requested).Should().Be(expected);

    [Theory]
    [InlineData(101)]
    [InlineData(1_000)]
    [InlineData(int.MaxValue)]
    public void ClampPageSize_CapsAnythingAboveTheMaximum(int requested) =>
        PagingLimits.ClampPageSize(requested).Should().Be(PagingLimits.MaxPageSize);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ClampPageSize_FallsBackToTheDefaultForNonPositiveValues(int requested) =>
        PagingLimits.ClampPageSize(requested).Should().Be(PagingLimits.DefaultPageSize);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void ClampPage_NeverReturnsAPageBeforeTheFirst(int requested, int expected) =>
        PagingLimits.ClampPage(requested).Should().Be(expected);
}
