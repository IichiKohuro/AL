namespace AL.Core;

/// <summary>Плодородная область. Медленно дрейфует, её плодородие меняется по сезонам.</summary>
public sealed class Oasis
{
    public float X { get; internal set; }
    public float Y { get; internal set; }
    public float Radius { get; internal init; }
    public float VelocityX { get; internal init; }
    public float VelocityY { get; internal init; }
    public float SeasonPhase { get; internal init; }

    /// <summary>Текущее плодородие: 1 — среднее, 0 — зима, 2 — пик лета.</summary>
    public float Fertility { get; internal set; } = 1f;
}
