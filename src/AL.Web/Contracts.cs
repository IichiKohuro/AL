using AL.Core;

namespace AL.Web;

public sealed record SimulationState(bool Paused, int Speed, int Tick, float TicksPerSecond, int Seed, int? SelectedId);

public sealed record StatsResponse(SimulationState State, WorldStats Stats, HistorySample[] History);

public sealed record ControlRequest(bool? Paused, int? Speed);

public sealed record ResetRequest(int? Seed);

public sealed record SelectRequest(float X, float Y);

public sealed record GenomeInfo(
    float Size,
    float Speed,
    float Vision,
    float FieldOfView,
    float Diet,
    float Hue,
    float MutationRate,
    float ReproductionThreshold,
    float ChildShare,
    float ClockRate);

public sealed record CreatureDetails(
    int Id,
    bool IsDead,
    int DeathTick,
    int Generation,
    int ParentId,
    int Age,
    int Lifespan,
    float Energy,
    float MaxEnergy,
    float MaxSpeed,
    int Children,
    int Kills,
    float PlantEnergyEaten,
    float MeatEnergyEaten,
    string DietClass,
    GenomeInfo Genome,
    float[] Inputs,
    float[] Hidden,
    float[] Outputs,
    float[] Weights)
{
    public static CreatureDetails From(Creature c)
    {
        var g = c.Genome;
        return new CreatureDetails(
            c.Id, c.IsDead, c.DeathTick, c.Generation, c.ParentId, c.Age, c.Lifespan,
            c.Energy, c.MaxEnergy, c.MaxSpeed, c.Children, c.Kills,
            c.PlantEnergyEaten, c.MeatEnergyEaten, c.DietClass.ToString(),
            new GenomeInfo(g.Size, g.Speed, g.Vision, g.FieldOfView, g.Diet, g.Hue,
                g.MutationRate, g.ReproductionThreshold, g.ChildShare, g.ClockRate),
            [.. c.Brain.Inputs], [.. c.Brain.Hidden], [.. c.Brain.Outputs], g.Weights.ToArray());
    }
}
