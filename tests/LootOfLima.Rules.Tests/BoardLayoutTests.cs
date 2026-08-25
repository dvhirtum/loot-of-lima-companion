namespace LootOfLima.Rules.Tests;

public class BoardLayoutTests
{
    private static readonly BoardLayout Layout = BoardLayout.Default;

    [Fact]
    public void Every_location_is_assigned_to_exactly_one_sector()
    {
        int[] perSector = new int[Directions.Count];
        foreach (LocationId location in LocationId.All)
        {
            perSector[(int)Layout.SectorOf(location)]++;
        }

        Assert.Equal(24, LocationId.All.Count);
        Assert.All(perSector, count => Assert.Equal(3, count));
    }

    [Fact]
    public void Each_sector_holds_one_location_of_each_terrain()
    {
        foreach (Direction sector in Directions.All)
        {
            Terrain[] terrains = Layout.LocationsIn(sector).Select(l => l.Terrain).Order().ToArray();
            Assert.Equal([Terrain.Beach, Terrain.Forest, Terrain.Mountain], terrains);
        }
    }

    [Fact]
    public void Coincident_arrows_sweep_the_whole_circle()
    {
        foreach (Direction direction in Directions.All)
        {
            Assert.Equal(Directions.Count, Layout.ArcSectorCount(direction, direction));
        }
    }

    [Fact]
    public void Adjacent_arrows_cover_exactly_the_start_sector()
    {
        foreach (Direction direction in Directions.All)
        {
            Assert.Equal(1, Layout.ArcSectorCount(direction, direction.Next()));
            Assert.True(Layout.ArcContains(direction, direction.Next(), direction));
        }
    }

    [Fact]
    public void Opposing_arcs_are_disjoint_and_together_cover_the_circle()
    {
        foreach (Direction a in Directions.All)
        {
            foreach (Direction b in Directions.All)
            {
                if (a == b)
                {
                    continue;
                }

                byte forward = Layout.ArcSectorMask(a, b);
                byte backward = Layout.ArcSectorMask(b, a);

                Assert.Equal(0, forward & backward);
                Assert.Equal(0xFF, forward | backward);
            }
        }
    }

    [Fact]
    public void Arc_is_inclusive_of_start_and_exclusive_of_end()
    {
        Assert.True(Layout.ArcContains(Direction.N, Direction.E, Direction.N));
        Assert.True(Layout.ArcContains(Direction.N, Direction.E, Direction.NE));
        Assert.False(Layout.ArcContains(Direction.N, Direction.E, Direction.E));
    }

    [Fact]
    public void Arcs_wrap_past_north()
    {
        Assert.Equal(3, Layout.ArcSectorCount(Direction.W, Direction.NE));
        Assert.True(Layout.ArcContains(Direction.W, Direction.NE, Direction.NW));
        Assert.True(Layout.ArcContains(Direction.W, Direction.NE, Direction.N));
        Assert.False(Layout.ArcContains(Direction.W, Direction.NE, Direction.NE));
    }
}
