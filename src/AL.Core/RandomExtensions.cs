namespace AL.Core;

internal static class RandomExtensions
{
    public static float NextFloat(this Random rng, float min, float max) => min + rng.NextSingle() * (max - min);

    /// <summary>Нормальное распределение N(0, 1), преобразование Бокса — Мюллера.</summary>
    public static float NextGaussian(this Random rng)
    {
        float u1 = 1f - rng.NextSingle();
        float u2 = rng.NextSingle();
        return MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(MathF.Tau * u2);
    }
}
