namespace AL.Core;

/// <summary>
/// Мозг существа: полносвязная сеть 28 → 16 → 6 с активацией tanh. Веса берутся из генома и не меняются
/// при жизни — учится не особь, а вид, через отбор.
/// </summary>
public sealed class Brain
{
    /// <summary>Поле зрения делится на секторы: 0 — крайний левый, 4 — крайний правый.</summary>
    public const int Sectors = 5;

    // Входы: для каждого сектора — близость ближайшего объекта (0 — не видно, 1 — вплотную).
    public const int InPlant = 0;
    public const int InMeat = InPlant + Sectors;
    public const int InCreature = InMeat + Sectors;
    /// <summary>Насколько ближайшее существо в секторе непохоже по цвету (0 — родня, 1 — чужой).</summary>
    public const int InOtherness = InCreature + Sectors;
    public const int InEnergy = InOtherness + Sectors;
    public const int InSpeed = InEnergy + 1;
    public const int InAge = InSpeed + 1;
    public const int InClock = InAge + 1;
    public const int InTouch = InClock + 1;
    public const int InPain = InTouch + 1;
    public const int InMemory = InPain + 1;
    public const int MemoryCount = 2;
    public const int InputCount = InMemory + MemoryCount;

    public const int HiddenCount = 16;

    // Выходы
    public const int OutThrust = 0;
    public const int OutTurn = 1;
    public const int OutAttack = 2;
    public const int OutReproduce = 3;
    /// <summary>Ячейки памяти: значения выходов возвращаются на входы в следующем тике.</summary>
    public const int OutMemory = 4;
    public const int OutputCount = OutMemory + MemoryCount;

    public const int WeightCount = (InputCount + 1) * HiddenCount + (HiddenCount + 1) * OutputCount;

    private readonly float[] _weights;

    public Brain(float[] weights)
    {
        if (weights.Length != WeightCount)
            throw new ArgumentException($"Ожидалось весов: {WeightCount}, получено: {weights.Length}.", nameof(weights));
        _weights = weights;
    }

    public float[] Inputs { get; } = new float[InputCount];
    public float[] Hidden { get; } = new float[HiddenCount];
    public float[] Outputs { get; } = new float[OutputCount];

    /// <summary>Прямой проход. Веса хранятся построчно: [смещение, веса входов] для каждого нейрона.</summary>
    public void Think()
    {
        ReadOnlySpan<float> w = _weights;
        int k = 0;

        for (int h = 0; h < HiddenCount; h++)
        {
            float sum = w[k++];
            var row = w.Slice(k, InputCount);
            k += InputCount;
            for (int i = 0; i < row.Length; i++)
                sum += row[i] * Inputs[i];
            Hidden[h] = MathF.Tanh(sum);
        }

        for (int o = 0; o < OutputCount; o++)
        {
            float sum = w[k++];
            var row = w.Slice(k, HiddenCount);
            k += HiddenCount;
            for (int h = 0; h < row.Length; h++)
                sum += row[h] * Hidden[h];
            Outputs[o] = MathF.Tanh(sum);
        }
    }
}
