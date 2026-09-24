namespace AL.Core.Tests;

public class WorldTests
{
    [Fact]
    public void SameSeed_GivesIdenticalWorlds()
    {
        // 300 существ — восприятие считается параллельно, результат всё равно должен совпасть.
        var a = new World(new SimulationSettings { Seed = 7 });
        var b = new World(new SimulationSettings { Seed = 7 });

        for (int i = 0; i < 300; i++)
        {
            a.Step();
            b.Step();
        }

        Assert.Equal(Snapshot(a), Snapshot(b));
    }

    [Fact]
    public void DifferentSeeds_GiveDifferentWorlds()
    {
        var a = new World(new SimulationSettings { Seed = 1 });
        var b = new World(new SimulationSettings { Seed = 2 });
        a.Step();
        b.Step();

        Assert.NotEqual(Snapshot(a), Snapshot(b));
    }

    [Fact]
    public void LongRun_KeepsInvariants()
    {
        var settings = new SimulationSettings { Seed = 11 };
        var world = new World(settings);

        for (int i = 0; i < 1500; i++)
        {
            world.Step();

            Assert.InRange(world.Creatures.Count, settings.MinCreatures, settings.MaxCreatures);
            Assert.True(world.ComputeStats().Plants <= settings.MaxPlants);
            foreach (var c in world.Creatures)
            {
                Assert.False(c.IsDead);
                Assert.True(c.Energy <= c.MaxEnergy + 1e-3f);
                Assert.InRange(c.X, 0f, settings.Width);
                Assert.InRange(c.Y, 0f, settings.Height);
            }
        }

        var stats = world.ComputeStats();
        Assert.True(stats.Births > 0);
        Assert.True(stats.MaxGeneration > 1);
    }

    [Fact]
    public void Herbivore_EatsPlantItTouches()
    {
        var world = TestGenomes.EmptyWorld();
        var creature = Place(world, TestGenomes.Create(diet: 0f), 100f, 100f, energy: 50f);
        world.AddFood(new Food(101f, 100f, world.Settings.PlantEnergy, FoodKind.Plant));

        world.Step();

        Assert.Empty(world.Food);
        Assert.True(creature.Energy > 50f + world.Settings.PlantEnergy * 0.9f);
        Assert.Equal(world.Settings.PlantEnergy, creature.PlantEnergyEaten, 3);
    }

    [Fact]
    public void Carnivore_IgnoresPlants()
    {
        var world = TestGenomes.EmptyWorld();
        var creature = Place(world, TestGenomes.Create(diet: 1f), 100f, 100f, energy: 50f);
        world.AddFood(new Food(101f, 100f, world.Settings.PlantEnergy, FoodKind.Plant));

        world.Step();

        Assert.Single(world.Food);
        Assert.True(creature.Energy < 50f);
    }

    [Fact]
    public void StarvingCreature_DiesAndLeavesMeat()
    {
        var world = TestGenomes.EmptyWorld();
        var creature = Place(world, TestGenomes.Create(), 100f, 100f, energy: 0.001f);

        world.Step();

        Assert.True(creature.IsDead);
        Assert.Empty(world.Creatures);
        Assert.NotEmpty(world.Food);
        Assert.All(world.Food, f => Assert.Equal(FoodKind.Meat, f.Kind));
        Assert.Equal(creature.BodyEnergy, world.Food.Sum(f => f.Energy), 3);
    }

    [Fact]
    public void WellFedAdult_WhoWantsTo_GivesBirthToMutatedChild()
    {
        var world = TestGenomes.EmptyWorld();
        var genome = TestGenomes.Create(weights: w => w[TestGenomes.OutputBias(Brain.OutReproduce)] = 2f);
        var parent = Place(world, genome, 100f, 100f, energy: 90f);
        parent.Age = world.Settings.MaturityAge;

        world.Step();

        Assert.Equal(2, world.Creatures.Count);
        var child = world.Creatures.Single(c => c != parent);
        Assert.Equal(1, child.Generation);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(1, parent.Children);
        Assert.True(parent.Energy < 90f - child.Energy);
        Assert.Equal(1, world.Counters.Births);
    }

    [Fact]
    public void Predator_BitesPreyInFrontAndGainsEnergy()
    {
        var world = TestGenomes.EmptyWorld();
        var predatorGenome = TestGenomes.Create(diet: 1f, weights: w => w[TestGenomes.OutputBias(Brain.OutAttack)] = 2f);
        var predator = Place(world, predatorGenome, 100f, 100f, energy: 50f);
        var prey = Place(world, TestGenomes.Create(diet: 0f), 100f + predator.Radius * 2 - 1f, 100f, energy: 50f);

        world.Step();

        Assert.True(predator.Biting);
        Assert.True(prey.Energy < 50f - 1f);
        Assert.True(predator.MeatEnergyEaten > 0f);
    }

    [Fact]
    public void Predator_CannotBiteBehindItself()
    {
        var world = TestGenomes.EmptyWorld();
        var predatorGenome = TestGenomes.Create(diet: 1f, weights: w => w[TestGenomes.OutputBias(Brain.OutAttack)] = 2f);
        var predator = Place(world, predatorGenome, 100f, 100f, energy: 50f);
        Place(world, TestGenomes.Create(diet: 0f), 100f - predator.Radius * 2 + 1f, 100f, energy: 50f);

        world.Step();

        Assert.False(predator.Biting);
    }

    [Fact]
    public void Creature_SeesPlantInCentralSector()
    {
        var world = TestGenomes.EmptyWorld();
        var creature = Place(world, TestGenomes.Create(), 100f, 100f, energy: 50f);
        world.AddFood(new Food(150f, 100f, world.Settings.PlantEnergy, FoodKind.Plant));

        world.Step();

        const int center = Brain.Sectors / 2;
        Assert.Equal(0.5f, creature.Brain.Inputs[Brain.InPlant + center], 3);
        for (int s = 0; s < Brain.Sectors; s++)
        {
            if (s != center)
                Assert.Equal(0f, creature.Brain.Inputs[Brain.InPlant + s]);
        }
    }

    private static Creature Place(World world, Genome genome, float x, float y, float energy)
    {
        var creature = world.Spawn(genome);
        creature.X = x;
        creature.Y = y;
        creature.Angle = 0f;
        creature.Energy = energy;
        return creature;
    }

    private static string Snapshot(World world) =>
        string.Join(";", world.Creatures.Select(c => $"{c.Id}:{c.X:R},{c.Y:R},{c.Energy:R}"))
        + $"|food={world.Food.Count}|tick={world.Tick}";
}
