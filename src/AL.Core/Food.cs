namespace AL.Core;

public enum FoodKind : byte
{
    Plant,
    Meat,
}

public struct Food(float x, float y, float energy, FoodKind kind)
{
    public const float Radius = 2.5f;

    public float X = x;
    public float Y = y;
    public float Energy = energy;
    public FoodKind Kind = kind;
    public int Age;
    public bool Eaten;
}
