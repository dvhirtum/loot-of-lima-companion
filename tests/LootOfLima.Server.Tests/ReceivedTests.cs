using Sut = LootOfLima.Server.Tests.Received<int>;

namespace LootOfLima.Server.Tests;

/// <summary>
/// The one place in this harness where two threads meet.
/// </summary>
/// <remarks>
/// <para>
/// Harness scaffolding is not usually worth testing, but scaffolding that can corrupt an assertion
/// is: a torn read here surfaces as a test failing for reasons that have nothing to do with the
/// server, which is the most expensive kind of failure to chase.
/// </para>
/// <para>
/// What is tested here is the half that can be pinned down: that a reader is handed a copy. The
/// mutual exclusion itself is not, and deliberately so. A stress test for it passed with the locks
/// taken out about two runs in three — the writer finishes in milliseconds while each snapshot
/// grows more expensive, so the readers back off exactly when contention would matter. A guard
/// against flakiness that is itself flaky is the disease, not the cure. The answer instead was to
/// make the surface small enough to read: four short methods in one file, every one of which takes
/// the same lock, and nothing else in the harness touching a list at all.
/// </para>
/// <para>
/// It sits at the root beside <see cref="HubHarness"/> rather than in a per-type folder, because
/// that is where the harness itself lives.
/// </para>
/// </remarks>
public class ReceivedTests
{
    [Fact]
    public void Should_hand_out_a_copy_when_a_snapshot_is_taken()
    {
        // Arrange
        // A caller enumerating the live list would be overtaken by the next frame arriving, which
        // is the failure this type exists to keep out of assertions.
        Sut received = new();
        received.Add(1);

        // Act
        IReadOnlyList<int> snapshot = received.Snapshot();
        received.Add(2);

        // Assert
        Assert.Equal([1], snapshot);
    }

    [Fact]
    public void Should_keep_arrival_order_when_several_have_arrived()
    {
        // Arrange
        Sut received = new();

        // Act
        foreach (int item in (int[])[3, 1, 2])
        {
            received.Add(item);
        }

        // Assert
        Assert.Equal([3, 1, 2], received.Snapshot());
        Assert.Equal(2, received.Last());
        Assert.Equal(3, received.Count);
    }

    [Fact]
    public void Should_report_nothing_when_nothing_has_arrived()
    {
        // Arrange
        // The harness asks before the first frame lands, so this is a real state rather than an
        // edge case: it is what every client looks like for its first few milliseconds.
        Sut received = new();

        // Act
        // Assert
        Assert.Equal(0, received.Count);
        Assert.Empty(received.Snapshot());
        Assert.Equal(0, received.Last());
    }
}
