namespace AL.Core.Tests;

public class BrainTests
{
    [Fact]
    public void Think_KeepsAllActivationsWithinTanhRange()
    {
        var rng = new Random(1);
        var weights = Enumerable.Range(0, Brain.WeightCount).Select(_ => rng.NextSingle() * 8f - 4f).ToArray();
        var brain = new Brain(weights);

        for (int i = 0; i < Brain.InputCount; i++)
            brain.Inputs[i] = rng.NextSingle() * 2f - 1f;
        brain.Think();

        Assert.All(brain.Hidden, v => Assert.InRange(v, -1f, 1f));
        Assert.All(brain.Outputs, v => Assert.InRange(v, -1f, 1f));
    }

    [Fact]
    public void Think_WithZeroWeights_OutputsZero()
    {
        var brain = new Brain(new float[Brain.WeightCount]);
        Array.Fill(brain.Inputs, 1f);

        brain.Think();

        Assert.All(brain.Outputs, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Think_UsesOutputBias()
    {
        var weights = new float[Brain.WeightCount];
        weights[TestGenomes.OutputBias(Brain.OutReproduce)] = 2f;
        var brain = new Brain(weights);

        brain.Think();

        Assert.Equal(MathF.Tanh(2f), brain.Outputs[Brain.OutReproduce], 5);
        Assert.Equal(0f, brain.Outputs[Brain.OutThrust]);
    }

    [Fact]
    public void Think_PropagatesInputThroughHiddenLayer()
    {
        // вход «энергия» → скрытый нейрон 0 → выход «тяга»
        var weights = new float[Brain.WeightCount];
        weights[1 + Brain.InEnergy] = 1f;
        weights[TestGenomes.OutputBias(Brain.OutThrust) + 1] = 1f;
        var brain = new Brain(weights);
        brain.Inputs[Brain.InEnergy] = 0.5f;

        brain.Think();

        Assert.Equal(MathF.Tanh(MathF.Tanh(0.5f)), brain.Outputs[Brain.OutThrust], 5);
    }

    [Fact]
    public void Constructor_RejectsWrongWeightCount() =>
        Assert.Throws<ArgumentException>(() => new Brain(new float[Brain.WeightCount - 1]));
}
