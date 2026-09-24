namespace AL.Core.Tests;

public class SpeciesTests
{
    [Fact]
    public void CreaturesSpawnedInOneTick_ShareSpecies_LaterSpawnsFoundNewOne()
    {
        var world = TestGenomes.EmptyWorld();
        var a = world.Spawn(TestGenomes.Create());
        var b = world.Spawn(TestGenomes.Create());
        world.Step();
        var c = world.Spawn(TestGenomes.Create());

        Assert.Same(a.Species, b.Species);
        Assert.NotSame(a.Species, c.Species);
        Assert.Equal(2, a.Species.Population);
        Assert.Equal(0, c.Species.ParentId);
    }

    [Fact]
    public void Child_InheritsParentSpecies()
    {
        var world = TestGenomes.EmptyWorld();
        var genome = TestGenomes.Create(weights: w => w[TestGenomes.OutputBias(Brain.OutReproduce)] = 2f);
        var parent = world.Spawn(genome);
        parent.Energy = 90f;
        parent.Age = world.Settings.MaturityAge;

        world.Step();

        var child = world.Creatures.Single(c => c != parent);
        Assert.Same(parent.Species, child.Species);
        Assert.Equal(2, parent.Species.Population);
        Assert.Equal(2, parent.Species.PeakPopulation);
    }

    [Fact]
    public void Species_GoesExtinct_WhenLastMemberDies()
    {
        var world = TestGenomes.EmptyWorld();
        var creature = world.Spawn(TestGenomes.Create());
        creature.Energy = 0.001f;
        var species = creature.Species;

        world.Step();

        Assert.True(species.IsExtinct);
        Assert.Equal(world.Tick, species.ExtinctTick);
        Assert.Equal(0, species.Population);
        Assert.Equal(new SpeciesSample(world.Tick, 0), species.History[^1]);
    }

    [Fact]
    public void Census_SplitsOffDivergentGroup_AsChildSpecies()
    {
        var world = CensusWorld();
        var herbivores = Enumerable.Range(0, 10).Select(_ => world.Spawn(TestGenomes.Create(diet: 0f))).ToList();
        var carnivores = Enumerable.Range(0, 5).Select(_ => world.Spawn(TestGenomes.Create(diet: 1f))).ToList();
        var original = herbivores[0].Species;

        world.Step();

        Assert.All(herbivores, c => Assert.Same(original, c.Species));
        var split = carnivores[0].Species;
        Assert.NotSame(original, split);
        Assert.All(carnivores, c => Assert.Same(split, c.Species));
        Assert.Equal(original.Id, split.ParentId);
        Assert.Equal(10, original.Population);
        Assert.Equal(5, split.Population);
        Assert.Equal(1f, split.AverageDiet, 3);
        Assert.False(original.IsExtinct);
    }

    [Fact]
    public void Census_KeepsTooSmallGroupInsideSpecies()
    {
        var world = CensusWorld();
        var herbivores = Enumerable.Range(0, 10).Select(_ => world.Spawn(TestGenomes.Create(diet: 0f))).ToList();
        var carnivores = Enumerable.Range(0, 2).Select(_ => world.Spawn(TestGenomes.Create(diet: 1f))).ToList();

        world.Step();

        Assert.All(carnivores, c => Assert.Same(herbivores[0].Species, c.Species));
        Assert.Single(world.Species);
    }

    [Fact]
    public void Census_RecordsHistoryForLivingSpecies()
    {
        var world = CensusWorld();
        var creature = world.Spawn(TestGenomes.Create());

        world.Step();
        world.Step();

        Assert.Equal(new[] { new SpeciesSample(1, 1), new SpeciesSample(2, 1) }, creature.Species.History);
    }

    [Fact]
    public void Names_AreStableReadableAndUnique()
    {
        var names = Enumerable.Range(1, 6299).Select(SpeciesNames.For).ToList();

        Assert.Equal(names, Enumerable.Range(1, 6299).Select(SpeciesNames.For));
        Assert.All(names, n => Assert.Matches("^[A-Z][a-z]{2,}$", n));
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Names_GetRomanSuffixAfterCombinationsRunOut()
    {
        Assert.EndsWith(" II", SpeciesNames.For(6300));
        Assert.EndsWith(" III", SpeciesNames.For(12600));
        Assert.NotEqual(SpeciesNames.For(6300), SpeciesNames.For(6301));
    }

    [Fact]
    public void TraitDistance_TreatsHueAsCircle()
    {
        var a = SpeciesTraits.Of(TestGenomes.Create());
        var b = (float[])a.Clone();
        var c = (float[])a.Clone();
        a[(int)Gene.Hue] = 0.01f;
        b[(int)Gene.Hue] = 0.99f;
        c[(int)Gene.Hue] = 0.5f;

        Assert.Equal(0f, SpeciesTraits.Distance(a, a));
        Assert.True(SpeciesTraits.Distance(a, b) < SpeciesTraits.Distance(a, c) / 10);
    }

    [Fact]
    public void WorldWithSpecies_StaysDeterministic()
    {
        var a = new World(new SimulationSettings { Seed = 3 });
        var b = new World(new SimulationSettings { Seed = 3 });
        for (int i = 0; i < 1000; i++)
        {
            a.Step();
            b.Step();
        }

        Assert.Equal(
            a.Species.Select(s => (s.Id, s.ParentId, s.FoundedTick, s.Population)),
            b.Species.Select(s => (s.Id, s.ParentId, s.FoundedTick, s.Population)));
        Assert.True(a.Species.Count > 1);
    }

    [Fact]
    public void Populations_MatchLivingCreatures()
    {
        var world = new World(new SimulationSettings { Seed = 9 });
        for (int i = 0; i < 1500; i++)
            world.Step();

        var counted = world.Creatures.GroupBy(c => c.Species.Id).ToDictionary(g => g.Key, g => g.Count());
        foreach (var s in world.Species)
        {
            Assert.Equal(counted.GetValueOrDefault(s.Id), s.Population);
            Assert.Equal(s.Population == 0, s.IsExtinct);
        }
    }

    private static World CensusWorld() => new(new SimulationSettings
    {
        InitialCreatures = 0,
        MinCreatures = 0,
        InitialPlants = 0,
        PlantGrowth = 0,
        OasisCount = 0,
        SpeciesInterval = 1,
        SpeciesThreshold = 0.1f,
    });
}
