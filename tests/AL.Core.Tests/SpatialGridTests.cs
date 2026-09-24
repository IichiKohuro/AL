namespace AL.Core.Tests;

public class SpatialGridTests
{
    private const float Width = 1000f;
    private const float Height = 600f;

    [Fact]
    public void Query_FindsEveryObjectInsideRadius_IncludingAcrossEdges()
    {
        var rng = new Random(5);
        var xs = new float[500];
        var ys = new float[500];
        for (int i = 0; i < xs.Length; i++)
        {
            xs[i] = rng.NextSingle() * Width;
            ys[i] = rng.NextSingle() * Height;
        }

        var grid = new SpatialGrid(Width, Height, 50f);
        grid.Build(xs, ys);
        var found = new List<int>();

        for (int q = 0; q < 200; q++)
        {
            float x = rng.NextSingle() * Width;
            float y = rng.NextSingle() * Height;
            float radius = 10f + rng.NextSingle() * 150f;
            grid.Query(x, y, radius, found);
            var candidates = found.ToHashSet();

            for (int i = 0; i < xs.Length; i++)
            {
                float dx = Torus.Delta(x, xs[i], Width);
                float dy = Torus.Delta(y, ys[i], Height);
                if (dx * dx + dy * dy <= radius * radius)
                    Assert.Contains(i, candidates);
            }

            Assert.Equal(candidates.Count, found.Count); // без повторов
        }
    }

    [Fact]
    public void Query_SeesNeighbourOnTheOtherSideOfTheWorld()
    {
        var grid = new SpatialGrid(Width, Height, 50f);
        grid.Build([Width - 3f], [300f]);
        var found = new List<int>();

        grid.Query(2f, 300f, 10f, found);

        Assert.Equal(new[] { 0 }, found);
    }

    [Fact]
    public void Query_WithRadiusLargerThanWorld_ReturnsEachObjectOnce()
    {
        var grid = new SpatialGrid(Width, Height, 50f);
        grid.Build([10f, 500f, 990f], [10f, 300f, 590f]);
        var found = new List<int>();

        grid.Query(500f, 300f, 5000f, found);

        Assert.Equal(new[] { 0, 1, 2 }, found.Order());
    }
}
