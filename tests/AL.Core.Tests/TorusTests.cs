namespace AL.Core.Tests;

public class TorusTests
{
    [Theory]
    [InlineData(100f, 200f, 100f)]
    [InlineData(10f, 1990f, -20f)]
    [InlineData(1990f, 10f, 20f)]
    public void Delta_TakesShortestWayAroundTheRing(float from, float to, float expected) =>
        Assert.Equal(expected, Torus.Delta(from, to, 2000f), 3);

    [Theory]
    [InlineData(-5f, 95f)]
    [InlineData(105f, 5f)]
    [InlineData(50f, 50f)]
    [InlineData(-250f, 50f)]
    public void Wrap_BringsValueIntoRange(float value, float expected) =>
        Assert.Equal(expected, Torus.Wrap(value, 100f), 3);

    [Fact]
    public void Wrap_NeverReturnsSizeItself()
    {
        float wrapped = Torus.Wrap(-1e-7f, 2000f);
        Assert.InRange(wrapped, 0f, 2000f);
        Assert.NotEqual(2000f, wrapped);
    }

    [Theory]
    [InlineData(3 * MathF.PI / 2, -MathF.PI / 2)]
    [InlineData(-3 * MathF.PI / 2, MathF.PI / 2)]
    [InlineData(0.5f, 0.5f)]
    public void WrapAngle_ReturnsAngleInMinusPiToPi(float angle, float expected) =>
        Assert.Equal(expected, Torus.WrapAngle(angle), 4);
}
