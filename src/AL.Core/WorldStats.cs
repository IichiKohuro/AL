namespace AL.Core;

/// <summary>Точка истории популяции для графика.</summary>
public readonly record struct HistorySample(int Tick, int Herbivores, int Omnivores, int Carnivores, int Plants, int Meat);

/// <summary>Накопительные счётчики с начала симуляции.</summary>
public sealed class WorldCounters
{
    public long Births { get; internal set; }
    public long Deaths { get; internal set; }
    public long Kills { get; internal set; }
    /// <summary>Случайные существа, добавленные, когда популяция падала ниже минимума.</summary>
    public long Immigrants { get; internal set; }
}

/// <summary>Снимок состояния мира: численность и средние значения генов.</summary>
public sealed record WorldStats(
    int Tick,
    int Population,
    int Plants,
    int Meat,
    int Herbivores,
    int Omnivores,
    int Carnivores,
    double AverageGeneration,
    int MaxGeneration,
    double AverageSize,
    double AverageSpeed,
    double AverageVision,
    double AverageFieldOfView,
    double AverageDiet,
    double AverageMutationRate,
    long Births,
    long Deaths,
    long Kills,
    long Immigrants);
