namespace AL.Core;

/// <summary>
/// Равномерная сетка на торе для быстрого поиска соседей. Перестраивается каждый тик за O(n):
/// индексы объектов раскладываются по ячейкам сортировкой подсчётом.
/// </summary>
public sealed class SpatialGrid
{
    private readonly int _cols;
    private readonly int _rows;
    private readonly float _cellWidth;
    private readonly float _cellHeight;
    private readonly int[] _cellStart;
    private readonly int[] _cursor;
    private int[] _items = [];
    private int[] _cellOf = [];

    public SpatialGrid(float width, float height, float cellSize)
    {
        // Ячейки подгоняются так, чтобы мир делился на них без остатка — иначе перенос через край тора неточен.
        _cols = Math.Max(1, (int)(width / cellSize));
        _rows = Math.Max(1, (int)(height / cellSize));
        _cellWidth = width / _cols;
        _cellHeight = height / _rows;
        _cellStart = new int[_cols * _rows + 1];
        _cursor = new int[_cols * _rows];
    }

    public void Build(ReadOnlySpan<float> xs, ReadOnlySpan<float> ys)
    {
        int count = xs.Length;
        if (_items.Length < count)
        {
            _items = new int[count * 2];
            _cellOf = new int[count * 2];
        }

        Array.Clear(_cellStart);
        for (int i = 0; i < count; i++)
        {
            int cell = CellY(ys[i]) * _cols + CellX(xs[i]);
            _cellOf[i] = cell;
            _cellStart[cell + 1]++;
        }

        for (int c = 0; c < _cursor.Length; c++)
            _cellStart[c + 1] += _cellStart[c];

        Array.Copy(_cellStart, _cursor, _cursor.Length);
        for (int i = 0; i < count; i++)
            _items[_cursor[_cellOf[i]]++] = i;
    }

    /// <summary>
    /// Кладёт в <paramref name="result"/> индексы объектов из ячеек, которые пересекает круг.
    /// Это кандидаты: точное расстояние проверяет вызывающий код.
    /// </summary>
    public void Query(float x, float y, float radius, List<int> result)
    {
        result.Clear();

        int spanX = (int)MathF.Ceiling(radius / _cellWidth);
        int spanY = (int)MathF.Ceiling(radius / _cellHeight);
        int cx = CellX(x);
        int cy = CellY(y);

        int x0 = cx - spanX, x1 = cx + spanX;
        int y0 = cy - spanY, y1 = cy + spanY;
        if (x1 - x0 + 1 >= _cols) (x0, x1) = (0, _cols - 1);
        if (y1 - y0 + 1 >= _rows) (y0, y1) = (0, _rows - 1);

        for (int gy = y0; gy <= y1; gy++)
        {
            int row = Mod(gy, _rows) * _cols;
            for (int gx = x0; gx <= x1; gx++)
            {
                int cell = row + Mod(gx, _cols);
                for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    result.Add(_items[k]);
            }
        }
    }

    private int CellX(float x) => Math.Clamp((int)(x / _cellWidth), 0, _cols - 1);

    private int CellY(float y) => Math.Clamp((int)(y / _cellHeight), 0, _rows - 1);

    private static int Mod(int value, int size)
    {
        int m = value % size;
        return m < 0 ? m + size : m;
    }
}
