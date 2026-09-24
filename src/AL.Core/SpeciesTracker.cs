namespace AL.Core;

/// <summary>Признаки для сравнения существ: гены, приведённые к диапазону [0, 1].</summary>
internal static class SpeciesTraits
{
    // Рацион весит больше всего: он определяет нишу. Цвет — нейтральная метка родства.
    private static readonly float[] Weights = CreateWeights();
    private static readonly float TotalWeight = Weights.Sum();

    public static float[] Of(Genome genome)
    {
        var traits = new float[Genome.GeneCount];
        for (int i = 0; i < traits.Length; i++)
        {
            var (min, max) = Genome.RangeOf((Gene)i);
            traits[i] = (genome[(Gene)i] - min) / (max - min);
        }
        return traits;
    }

    /// <summary>Взвешенное среднее отличие признаков: 0 — одинаковые, 1 — противоположные.</summary>
    public static float Distance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float d = MathF.Abs(a[i] - b[i]);
            if (i == (int)Gene.Hue)
                d = MathF.Min(d, 1f - d) * 2f;
            sum += Weights[i] * d;
        }
        return sum / TotalWeight;
    }

    private static float[] CreateWeights()
    {
        var weights = new float[Genome.GeneCount];
        weights[(int)Gene.Size] = 1.5f;
        weights[(int)Gene.Speed] = 1f;
        weights[(int)Gene.Vision] = 1f;
        weights[(int)Gene.FieldOfView] = 1f;
        weights[(int)Gene.Diet] = 3f;
        weights[(int)Gene.Hue] = 2f;
        weights[(int)Gene.MutationRate] = 0.5f;
        weights[(int)Gene.ReproductionThreshold] = 0.5f;
        weights[(int)Gene.ChildShare] = 0.5f;
        weights[(int)Gene.ClockRate] = 0.5f;
        return weights;
    }
}

/// <summary>
/// Ведёт учёт видов. Раз в <see cref="SimulationSettings.SpeciesInterval"/> тиков проводит перепись:
/// считает средний облик каждого вида, и существа, ушедшие от него дальше порога, группирует между собой.
/// Группа от <see cref="SimulationSettings.MinSpeciesSplit"/> особей становится новым видом.
/// </summary>
internal sealed class SpeciesTracker
{
    private readonly List<Species> _all = [];
    private readonly List<Species> _alive = [];
    private int _nextId = 1;

    public IReadOnlyList<Species> All => _all;

    public Species Found(int parentId, int tick)
    {
        var species = new Species(_nextId++, parentId, tick);
        _all.Add(species);
        _alive.Add(species);
        return species;
    }

    public void Census(IReadOnlyList<Creature> creatures, int tick, float threshold, int minSplit)
    {
        _alive.RemoveAll(s => s.IsExtinct);

        foreach (var s in _alive)
            s.BeginCensus();
        foreach (var c in creatures)
            c.Species.CountMember(c);
        foreach (var s in _alive)
            s.EndCensus();

        // Отбившихся собираем в группы вокруг «лидеров» — только внутри своего вида.
        var groups = new List<Group>();
        foreach (var c in creatures)
        {
            var own = c.Species;
            if (SpeciesTraits.Distance(c.Traits, own.Centroid) <= threshold)
                continue;

            Group? target = null;
            float best = threshold;
            foreach (var group in groups)
            {
                if (group.Parent != own)
                    continue;
                float d = SpeciesTraits.Distance(c.Traits, group.Leader.Traits);
                if (d <= best)
                {
                    best = d;
                    target = group;
                }
            }

            if (target is null)
            {
                target = new Group(own, c);
                groups.Add(target);
            }
            target.Members.Add(c);
        }

        foreach (var group in groups)
        {
            if (group.Members.Count < minSplit)
                continue;

            var species = Found(group.Parent.Id, tick);
            species.StartFrom(group.Leader);
            foreach (var c in group.Members)
            {
                c.Species.Remove(tick);
                species.Add();
                c.Species = species;
            }
        }

        _alive.RemoveAll(s => s.IsExtinct);
        foreach (var s in _alive)
            s.Record(tick);
    }

    private sealed class Group(Species parent, Creature leader)
    {
        public Species Parent { get; } = parent;
        public Creature Leader { get; } = leader;
        public List<Creature> Members { get; } = [];
    }
}

/// <summary>
/// Латинообразные имена видов. Номер переставляется по модулю числа сочетаний слогов,
/// поэтому имена выглядят случайными, но не повторяются; после исчерпания сочетаний
/// к имени добавляется номер круга: Ralumis II.
/// </summary>
internal static class SpeciesNames
{
    private static readonly string[] Starts =
        ["Ra", "Lu", "Mi", "To", "Ka", "Ve", "No", "Xi", "Pa", "Do", "Ul", "Sa", "Fe", "Gri", "Mo", "Ta",
         "Ze", "Bi", "Or", "El", "Qua", "Ny", "Sto", "Vi", "Cor", "Del", "Hy", "Ly", "Tre", "Ar"];
    private static readonly string[] Middles =
        ["", "ra", "lu", "mi", "to", "ka", "ve", "no", "xi", "pa", "do", "sa", "fe", "mo", "ta", "ze", "bi", "ri", "la", "ne", "vo"];
    private static readonly string[] Endings = ["us", "a", "is", "on", "ex", "ix", "um", "or", "ax", "es"];

    private static readonly int Combinations = Starts.Length * Middles.Length * Endings.Length;

    // Взаимно просто с числом сочетаний (6300 = 2²·3²·5²·7), поэтому перестановка без повторов.
    private const int Step = 4099;

    public static string For(int id)
    {
        int index = (int)((long)id * Step % Combinations);
        string name = Starts[index % Starts.Length];
        index /= Starts.Length;
        name += Middles[index % Middles.Length];
        index /= Middles.Length;
        name += Endings[index];

        int round = id / Combinations;
        return round == 0 ? name : $"{name} {ToRoman(round + 1)}";
    }

    private static string ToRoman(int number)
    {
        (int Value, string Numeral)[] numerals =
            [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
             (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var result = new System.Text.StringBuilder();
        foreach (var (value, numeral) in numerals)
        {
            while (number >= value)
            {
                result.Append(numeral);
                number -= value;
            }
        }
        return result.ToString();
    }
}
