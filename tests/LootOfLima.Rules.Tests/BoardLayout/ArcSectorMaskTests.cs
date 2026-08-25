using Sut = LootOfLima.Rules.BoardLayout;

namespace LootOfLima.Rules.Tests.BoardLayout;

public class ArcSectorMaskTests
{
    [Fact]
    public void Should_be_disjoint_and_cover_the_circle_when_an_arc_and_its_reverse_are_combined()
    {
        // Arrange
        Sut layout = Sut.Default;

        foreach (Direction a in Directions.All)
        {
            foreach (Direction b in Directions.All)
            {
                if (a == b)
                {
                    continue;
                }

                // Act
                byte forward = layout.ArcSectorMask(a, b);
                byte backward = layout.ArcSectorMask(b, a);

                // Assert
                Assert.Equal(0, forward & backward);
                Assert.Equal(0xFF, forward | backward);
            }
        }
    }
}
