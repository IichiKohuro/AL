namespace AL.Core;

/// <summary>Параметры мира. Время измеряется в тиках, расстояния — в условных единицах.</summary>
public sealed record SimulationSettings
{
    public int Seed { get; init; } = 1;

    // Мир — тор: выходя за край, существо появляется с другой стороны.
    public float Width { get; init; } = 2000f;
    public float Height { get; init; } = 1250f;

    // Популяция
    public int InitialCreatures { get; init; } = 300;
    public int MinCreatures { get; init; } = 40;
    public int MaxCreatures { get; init; } = 2000;

    // Растения растут в дрейфующих оазисах, у каждого оазиса свои «времена года».
    public int InitialPlants { get; init; } = 1500;
    public int MaxPlants { get; init; } = 3000;
    public float PlantGrowth { get; init; } = 8f;
    public float PlantEnergy { get; init; } = 12f;
    public int OasisCount { get; init; } = 5;
    public float OasisShare { get; init; } = 0.7f;
    public int SeasonLength { get; init; } = 6000;
    public float SeasonStrength { get; init; } = 0.8f;
    public float OasisDrift { get; init; } = 0.05f;

    // Мясо остаётся после смерти и со временем исчезает.
    public float MeatChunkEnergy { get; init; } = 15f;
    public int MeatLifetime { get; init; } = 2000;

    // Существа
    public int MaturityAge { get; init; } = 200;
    public int Lifespan { get; init; } = 3600;
    public int ReproductionCooldown { get; init; } = 90;
    public float MaxTurnRate { get; init; } = 0.12f;
    public float BaseMetabolism { get; init; } = 0.05f;
    public float MoveCost { get; init; } = 0.05f;
    public float VisionCost { get; init; } = 0.0001f;
    public float FieldOfViewCost { get; init; } = 0.004f;
    public float AttackCost { get; init; } = 0.05f;
    public float BiteDamage { get; init; } = 2f;

    /// <summary>Минимальная эффективность пищеварения, при которой существо вообще ест этот вид пищи.</summary>
    public float MinDigestion { get; init; } = 0.25f;

    // Виды: раз в SpeciesInterval тиков перепись; группа от MinSpeciesSplit особей,
    // ушедшая от среднего облика вида дальше SpeciesThreshold, становится новым видом.
    public int SpeciesInterval { get; init; } = 200;
    public float SpeciesThreshold { get; init; } = 0.06f;
    public int MinSpeciesSplit { get; init; } = 3;

    /// <summary>Как часто (в тиках) записывать точку в историю популяции.</summary>
    public int HistoryInterval { get; init; } = 25;
    public int HistoryCapacity { get; init; } = 4000;
}
