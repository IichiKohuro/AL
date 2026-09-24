namespace AL.Core.Tests;

public class GenomeTests
{
    [Fact]
    public void CreateRandom_ProducesGenesWithinRanges()
    {
        var rng = new Random(3);
        for (int i = 0; i < 200; i++)
            AssertWithinRanges(Genome.CreateRandom(rng));
    }

    [Fact]
    public void Mutate_KeepsGenesWithinRanges_OverManyGenerations()
    {
        var rng = new Random(4);
        var genome = Genome.CreateRandom(rng);
        for (int i = 0; i < 2000; i++)
        {
            genome = genome.Mutate(rng);
            AssertWithinRanges(genome);
        }
    }

    [Fact]
    public void Mutate_DoesNotChangeParent()
    {
        var rng = new Random(5);
        var parent = Genome.CreateRandom(rng);
        var weightsBefore = parent.Weights.ToArray();
        var genesBefore = Enum.GetValues<Gene>().Select(g => parent[g]).ToArray();

        for (int i = 0; i < 50; i++)
            parent.Mutate(rng);

        Assert.Equal(weightsBefore, parent.Weights.ToArray());
        Assert.Equal(genesBefore, Enum.GetValues<Gene>().Select(g => parent[g]).ToArray());
    }

    [Fact]
    public void Mutate_ChildColorStaysCloseToParent()
    {
        var rng = new Random(6);
        var parent = TestGenomes.Create();
        for (int i = 0; i < 100; i++)
        {
            float hue = parent.Mutate(rng).Hue;
            float distance = MathF.Min(MathF.Abs(hue - parent.Hue), 1f - MathF.Abs(hue - parent.Hue));
            Assert.True(distance < 0.1f, $"Цвет потомка {hue} слишком далёк от родительского {parent.Hue}");
        }
    }

    [Fact]
    public void FromValues_ClampsOutOfRangeValues()
    {
        var genes = Enumerable.Repeat(1000f, Genome.GeneCount).ToArray();
        var weights = Enumerable.Repeat(-1000f, Brain.WeightCount).ToArray();

        var genome = Genome.FromValues(genes, weights);

        AssertWithinRanges(genome);
    }

    [Fact]
    public void FromValues_RejectsWrongLengths()
    {
        Assert.Throws<ArgumentException>(() => Genome.FromValues(new float[1], new float[Brain.WeightCount]));
        Assert.Throws<ArgumentException>(() => Genome.FromValues(new float[Genome.GeneCount], new float[1]));
    }

    private static void AssertWithinRanges(Genome genome)
    {
        foreach (var gene in Enum.GetValues<Gene>())
        {
            var (min, max) = Genome.RangeOf(gene);
            Assert.InRange(genome[gene], min, max);
        }

        Assert.Equal(Brain.WeightCount, genome.Weights.Length);
        foreach (float w in genome.Weights)
            Assert.InRange(w, -4f, 4f);
    }
}
