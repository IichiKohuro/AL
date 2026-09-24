namespace AL.Core;

/// <summary>Геометрия мира-тора и работа с углами.</summary>
public static class Torus
{
    /// <summary>Кратчайшее смещение от <paramref name="from"/> до <paramref name="to"/> по кольцу длины <paramref name="size"/>.</summary>
    public static float Delta(float from, float to, float size)
    {
        float d = to - from;
        float half = size * 0.5f;
        if (d > half) d -= size;
        else if (d < -half) d += size;
        return d;
    }

    /// <summary>Приводит координату к диапазону [0, size).</summary>
    public static float Wrap(float value, float size)
    {
        if (value < 0) value += size;
        else if (value >= size) value -= size;

        // Страховка от больших шагов и ошибок округления вроде -1e-7 + size == size.
        if (value < 0 || value >= size)
        {
            value %= size;
            if (value < 0) value += size;
            if (value >= size) value = 0;
        }
        return value;
    }

    /// <summary>Приводит угол к диапазону (-π, π].</summary>
    public static float WrapAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle <= -MathF.PI) angle += MathF.Tau;
        return angle;
    }
}
