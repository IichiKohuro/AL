namespace AL.Core;

public enum DietClass
{
    Herbivore,
    Omnivore,
    Carnivore,
}

public sealed class Creature
{
    public const float RadiusPerSize = 5f;
    public const float MaxRadius = RadiusPerSize * Genome.MaxSize;
    public const float EnergyPerSize2 = 100f;
    public const float BodyEnergyPerSize2 = 20f;
    public const float BaseSpeed = 2.4f;

    internal Creature(int id, Genome genome, int generation, int parentId, int lifespan, int birthTick)
    {
        Id = id;
        Genome = genome;
        Generation = generation;
        ParentId = parentId;
        Lifespan = lifespan;
        BirthTick = birthTick;
        Brain = new Brain(genome.WeightArray);

        Radius = RadiusPerSize * genome.Size;
        MaxEnergy = EnergyPerSize2 * genome.Size * genome.Size;
        BodyEnergy = BodyEnergyFor(genome);
        MaxSpeed = BaseSpeed * genome.Speed / MathF.Pow(genome.Size, 0.25f);
    }

    public int Id { get; }
    public Genome Genome { get; }
    public Brain Brain { get; }
    public int Generation { get; }
    public int ParentId { get; }
    public int Lifespan { get; }
    public int BirthTick { get; }

    public float Radius { get; }
    public float MaxEnergy { get; }
    /// <summary>Энергия, «вложенная» в тело. Родитель платит её при рождении, после смерти она превращается в мясо.</summary>
    public float BodyEnergy { get; }
    public float MaxSpeed { get; }

    public DietClass DietClass => Classify(Genome.Diet);

    public float X { get; internal set; }
    public float Y { get; internal set; }
    public float Angle { get; internal set; }
    public float Speed { get; internal set; }
    public float Energy { get; internal set; }
    public int Age { get; internal set; }
    public int Children { get; internal set; }
    public int Kills { get; internal set; }
    public float PlantEnergyEaten { get; internal set; }
    public float MeatEnergyEaten { get; internal set; }
    /// <summary>Укусило кого-то в этом тике.</summary>
    public bool Biting { get; internal set; }
    public bool IsDead { get; internal set; }
    public int DeathTick { get; internal set; }

    internal float Pain;
    internal float ClockPhase;
    internal int ReproductionCooldown;

    public static float BodyEnergyFor(Genome genome) => BodyEnergyPerSize2 * genome.Size * genome.Size;

    public static DietClass Classify(float diet) => diet switch
    {
        < 1f / 3 => DietClass.Herbivore,
        > 2f / 3 => DietClass.Carnivore,
        _ => DietClass.Omnivore,
    };
}
