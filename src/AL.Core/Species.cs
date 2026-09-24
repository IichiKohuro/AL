namespace AL.Core;

/// <summary>Точка истории численности вида.</summary>
public readonly record struct SpeciesSample(int Tick, int Population);

/// <summary>
/// Вид — группа родственных существ со схожими признаками. Потомок наследует вид родителя;
/// когда часть вида заметно уходит от его «среднего» облика, она отделяется в новый вид.
/// </summary>
public sealed class Species
{
    private readonly List<SpeciesSample> _history = [];
    private readonly float[] _sum = new float[Genome.GeneCount];
    private float _hueX;
    private float _hueY;
    private float _dietSum;
    private int _count;

    internal Species(int id, int parentId, int foundedTick)
    {
        Id = id;
        ParentId = parentId;
        FoundedTick = foundedTick;
        Name = SpeciesNames.For(id);
    }

    public int Id { get; }
    /// <summary>Вид, от которого отделился этот; 0 — у вида нет предков (первое поколение или переселенцы).</summary>
    public int ParentId { get; }
    public string Name { get; }
    public int FoundedTick { get; }
    public int? ExtinctTick { get; private set; }
    public bool IsExtinct => ExtinctTick.HasValue;
    public int Population { get; private set; }
    public int PeakPopulation { get; private set; }
    public float AverageDiet { get; private set; }
    public IReadOnlyList<SpeciesSample> History => _history;

    /// <summary>Средние признаки членов вида на момент последней переписи (см. <see cref="SpeciesTraits"/>).</summary>
    internal float[] Centroid { get; } = new float[Genome.GeneCount];

    internal void Add()
    {
        Population++;
        if (Population > PeakPopulation)
            PeakPopulation = Population;
    }

    internal void Remove(int tick)
    {
        Population--;
        if (Population > 0)
            return;

        ExtinctTick = tick;
        _history.Add(new SpeciesSample(tick, 0));
    }

    internal void Record(int tick) => _history.Add(new SpeciesSample(tick, Population));

    internal void BeginCensus()
    {
        Array.Clear(_sum);
        _hueX = _hueY = _dietSum = 0;
        _count = 0;
    }

    internal void CountMember(Creature member)
    {
        var traits = member.Traits;
        for (int i = 0; i < traits.Length; i++)
            _sum[i] += traits[i];

        // Цвет — величина круговая, его среднее считается через углы.
        float angle = traits[(int)Gene.Hue] * MathF.Tau;
        _hueX += MathF.Cos(angle);
        _hueY += MathF.Sin(angle);
        _dietSum += member.Genome.Diet;
        _count++;
    }

    internal void EndCensus()
    {
        if (_count == 0)
            return;

        for (int i = 0; i < Centroid.Length; i++)
            Centroid[i] = _sum[i] / _count;

        float hue = MathF.Atan2(_hueY, _hueX) / MathF.Tau;
        Centroid[(int)Gene.Hue] = hue < 0 ? hue + 1f : hue;
        AverageDiet = _dietSum / _count;
    }

    /// <summary>Новый вид получает облик своего первого члена — до ближайшей переписи.</summary>
    internal void StartFrom(Creature founder)
    {
        founder.Traits.CopyTo(Centroid, 0);
        AverageDiet = founder.Genome.Diet;
    }
}
