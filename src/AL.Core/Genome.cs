namespace AL.Core;

public enum Gene
{
    /// <summary>Размер тела: запас энергии, сила укуса, но и расход.</summary>
    Size,
    /// <summary>Множитель максимальной скорости.</summary>
    Speed,
    /// <summary>Дальность зрения.</summary>
    Vision,
    /// <summary>Угол обзора, радианы.</summary>
    FieldOfView,
    /// <summary>Рацион: 0 — травоядное, 1 — хищник.</summary>
    Diet,
    /// <summary>Цвет. Мутирует понемногу при каждом рождении, поэтому родственники похожи по цвету.</summary>
    Hue,
    /// <summary>Вероятность мутации каждого гена и веса. Эволюционирует сама.</summary>
    MutationRate,
    /// <summary>Доля от максимальной энергии, после которой существо может размножаться.</summary>
    ReproductionThreshold,
    /// <summary>Доля энергии, которую родитель отдаёт потомку.</summary>
    ChildShare,
    /// <summary>Частота внутренних «часов» — входа, который колеблется сам по себе.</summary>
    ClockRate,
}

/// <summary>Геном: параметры тела и веса нейросети. Неизменяемый — мутация создаёт новый геном.</summary>
public sealed class Genome
{
    public const float MinSize = 0.6f;
    public const float MaxSize = 1.8f;
    private const float MaxWeight = 4f;

    private static readonly (float Min, float Max)[] Ranges =
    [
        (MinSize, MaxSize), // Size
        (0.5f, 1.5f),       // Speed
        (50f, 220f),        // Vision
        (0.5f, 5.5f),       // FieldOfView
        (0f, 1f),           // Diet
        (0f, 1f),           // Hue
        (0.005f, 0.25f),    // MutationRate
        (0.3f, 0.95f),      // ReproductionThreshold
        (0.15f, 0.7f),      // ChildShare
        (0.005f, 0.2f),     // ClockRate
    ];

    public static int GeneCount => Ranges.Length;

    private readonly float[] _genes;
    private readonly float[] _weights;

    private Genome(float[] genes, float[] weights)
    {
        _genes = genes;
        _weights = weights;
    }

    public float this[Gene gene] => _genes[(int)gene];

    public float Size => this[Gene.Size];
    public float Speed => this[Gene.Speed];
    public float Vision => this[Gene.Vision];
    public float FieldOfView => this[Gene.FieldOfView];
    public float Diet => this[Gene.Diet];
    public float Hue => this[Gene.Hue];
    public float MutationRate => this[Gene.MutationRate];
    public float ReproductionThreshold => this[Gene.ReproductionThreshold];
    public float ChildShare => this[Gene.ChildShare];
    public float ClockRate => this[Gene.ClockRate];

    public ReadOnlySpan<float> Weights => _weights;

    internal float[] WeightArray => _weights;

    public static (float Min, float Max) RangeOf(Gene gene) => Ranges[(int)gene];

    /// <summary>Случайный геном «первого поколения» со случайным мозгом.</summary>
    public static Genome CreateRandom(Random rng)
    {
        var genes = new float[GeneCount];
        genes[(int)Gene.Size] = rng.NextFloat(0.8f, 1.2f);
        genes[(int)Gene.Speed] = rng.NextFloat(0.8f, 1.2f);
        genes[(int)Gene.Vision] = rng.NextFloat(90f, 160f);
        genes[(int)Gene.FieldOfView] = rng.NextFloat(1.5f, 3.5f);
        // Четверть первого поколения — мясоеды: им есть чем питаться после первой волны смертей,
        // а дальше отбор решит, выживет ли эта ветка.
        genes[(int)Gene.Diet] = rng.NextSingle() < 0.25f ? rng.NextFloat(0.8f, 1f) : rng.NextFloat(0f, 0.2f);
        genes[(int)Gene.Hue] = rng.NextSingle();
        genes[(int)Gene.MutationRate] = rng.NextFloat(0.03f, 0.1f);
        genes[(int)Gene.ReproductionThreshold] = rng.NextFloat(0.5f, 0.8f);
        genes[(int)Gene.ChildShare] = rng.NextFloat(0.3f, 0.5f);
        genes[(int)Gene.ClockRate] = rng.NextFloat(0.01f, 0.1f);

        var weights = new float[Brain.WeightCount];
        for (int i = 0; i < weights.Length; i++)
            weights[i] = rng.NextFloat(-1f, 1f);

        return new Genome(genes, weights);
    }

    /// <summary>Собирает геном из готовых значений (загрузка, тесты). Значения вне диапазонов обрезаются.</summary>
    public static Genome FromValues(IReadOnlyList<float> genes, IReadOnlyList<float> weights)
    {
        if (genes.Count != GeneCount)
            throw new ArgumentException($"Ожидалось генов: {GeneCount}, получено: {genes.Count}.", nameof(genes));
        if (weights.Count != Brain.WeightCount)
            throw new ArgumentException($"Ожидалось весов: {Brain.WeightCount}, получено: {weights.Count}.", nameof(weights));

        var g = new float[GeneCount];
        for (int i = 0; i < g.Length; i++)
            g[i] = Math.Clamp(genes[i], Ranges[i].Min, Ranges[i].Max);

        var w = new float[weights.Count];
        for (int i = 0; i < w.Length; i++)
            w[i] = Math.Clamp(weights[i], -MaxWeight, MaxWeight);

        return new Genome(g, w);
    }

    /// <summary>Копия генома с мутациями.</summary>
    public Genome Mutate(Random rng)
    {
        float rate = MutationRate;

        var genes = (float[])_genes.Clone();
        for (int i = 0; i < genes.Length; i++)
        {
            if (i == (int)Gene.Hue)
            {
                // Цвет дрейфует всегда: так видно, кто кому родственник.
                float hue = (genes[i] + rng.NextGaussian() * 0.01f) % 1f;
                genes[i] = hue < 0 ? hue + 1f : hue;
                continue;
            }

            if (rng.NextSingle() >= rate)
                continue;

            var (min, max) = Ranges[i];
            genes[i] = Math.Clamp(genes[i] + rng.NextGaussian() * 0.08f * (max - min), min, max);
        }

        var weights = (float[])_weights.Clone();
        for (int i = 0; i < weights.Length; i++)
        {
            if (rng.NextSingle() >= rate)
                continue;

            weights[i] = rng.NextSingle() < 0.05f
                ? rng.NextFloat(-1f, 1f)
                : Math.Clamp(weights[i] + rng.NextGaussian() * 0.3f, -MaxWeight, MaxWeight);
        }

        return new Genome(genes, weights);
    }
}
