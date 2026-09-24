using System.Buffers.Binary;
using AL.Core;

namespace AL.Web;

/// <summary>
/// Бинарный кадр для браузера, little-endian:
/// <code>
/// u8 версия, i32 тик, f32 ширина, f32 высота
/// u8 оазисов  × (f32 x, f32 y, f32 радиус, f32 плодородие)
/// i32 существ × (f32 x, f32 y, u8 угол, u8 радиус×10, u8 цвет, u8 рацион, u8 энергия, u8 флаги)
/// i32 еды     × (u16 x, u16 y, u8 вид)
/// </code>
/// </summary>
public static class FrameEncoder
{
    public const byte Version = 1;
    public const byte FlagBiting = 1;
    public const byte FlagSelected = 2;

    private const int HeaderBytes = 1 + 4 + 4 + 4;
    private const int OasisBytes = 16;
    private const int CreatureBytes = 14;
    private const int FoodBytes = 5;

    public static byte[] Encode(World world, Creature? selected)
    {
        var oases = world.Oases;
        var creatures = world.Creatures;
        var food = world.Food;

        var data = new byte[HeaderBytes
                            + 1 + oases.Count * OasisBytes
                            + 4 + creatures.Count * CreatureBytes
                            + 4 + food.Count * FoodBytes];
        var writer = new Writer(data);

        writer.WriteByte(Version);
        writer.WriteInt32(world.Tick);
        writer.WriteSingle(world.Settings.Width);
        writer.WriteSingle(world.Settings.Height);

        writer.WriteByte((byte)oases.Count);
        for (int i = 0; i < oases.Count; i++)
        {
            var o = oases[i];
            writer.WriteSingle(o.X);
            writer.WriteSingle(o.Y);
            writer.WriteSingle(o.Radius);
            writer.WriteSingle(o.Fertility);
        }

        writer.WriteInt32(creatures.Count);
        for (int i = 0; i < creatures.Count; i++)
        {
            var c = creatures[i];
            byte flags = 0;
            if (c.Biting)
                flags |= FlagBiting;
            if (ReferenceEquals(c, selected))
                flags |= FlagSelected;

            writer.WriteSingle(c.X);
            writer.WriteSingle(c.Y);
            writer.WriteByte(ToByte((c.Angle + MathF.PI) / MathF.Tau));
            writer.WriteByte((byte)MathF.Round(c.Radius * 10f));
            writer.WriteByte(ToByte(c.Genome.Hue));
            writer.WriteByte(ToByte(c.Genome.Diet));
            writer.WriteByte(ToByte(c.Energy / c.MaxEnergy));
            writer.WriteByte(flags);
        }

        writer.WriteInt32(food.Count);
        for (int i = 0; i < food.Count; i++)
        {
            var f = food[i];
            writer.WriteUInt16((ushort)f.X);
            writer.WriteUInt16((ushort)f.Y);
            writer.WriteByte((byte)f.Kind);
        }

        return data;
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);

    private ref struct Writer(Span<byte> buffer)
    {
        private readonly Span<byte> _buffer = buffer;
        private int _position;

        public void WriteByte(byte value) => _buffer[_position++] = value;

        public void WriteUInt16(ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer[_position..], value);
            _position += 2;
        }

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_buffer[_position..], value);
            _position += 4;
        }

        public void WriteSingle(float value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(_buffer[_position..], value);
            _position += 4;
        }
    }
}
