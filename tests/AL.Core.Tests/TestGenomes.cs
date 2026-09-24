namespace AL.Core.Tests;

internal static class TestGenomes
{
    /// <summary>Геном с предсказуемыми генами. По умолчанию все веса нулевые: существо стоит на месте и ничего не делает.</summary>
    public static Genome Create(float diet = 0f, float size = 1f, Action<float[]>? weights = null)
    {
        var genes = new float[Genome.GeneCount];
        genes[(int)Gene.Size] = size;
        genes[(int)Gene.Speed] = 1f;
        genes[(int)Gene.Vision] = 100f;
        genes[(int)Gene.FieldOfView] = 2f;
        genes[(int)Gene.Diet] = diet;
        genes[(int)Gene.Hue] = 0.5f;
        genes[(int)Gene.MutationRate] = 0.05f;
        genes[(int)Gene.ReproductionThreshold] = 0.5f;
        genes[(int)Gene.ChildShare] = 0.4f;
        genes[(int)Gene.ClockRate] = 0.05f;

        var w = new float[Brain.WeightCount];
        weights?.Invoke(w);
        return Genome.FromValues(genes, w);
    }

    /// <summary>Индекс смещения (bias) выходного нейрона в массиве весов.</summary>
    public static int OutputBias(int output) =>
        (Brain.InputCount + 1) * Brain.HiddenCount + output * (Brain.HiddenCount + 1);

    /// <summary>Мир без случайных существ и растений — для точечных проверок правил.</summary>
    public static World EmptyWorld() => new(new SimulationSettings
    {
        InitialCreatures = 0,
        MinCreatures = 0,
        InitialPlants = 0,
        PlantGrowth = 0,
        OasisCount = 0,
    });
}
