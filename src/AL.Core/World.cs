using System.Runtime.InteropServices;

namespace AL.Core;

/// <summary>
/// Мир и его правила. Один вызов <see cref="Step"/> — один тик:
/// восприятие и «мышление» всех существ (параллельно), затем действия (последовательно, в порядке списка).
/// При одинаковом сиде результат детерминирован.
/// </summary>
public sealed class World
{
    private const float CellSize = 50f;
    private const float ReverseThrust = 0.3f;
    private const float Acceleration = 0.2f;
    private const int ParallelThreshold = 64;

    // Координаты в сетке берутся на начало тика; к моменту проверки контакта объект мог сдвинуться.
    private const float MovementMargin = 5f;

    [ThreadStatic] private static List<int>? t_buffer;

    private readonly Random _rng;
    private readonly List<Creature> _creatures = [];
    private readonly List<Creature> _newborns = [];
    private readonly List<Food> _food = [];
    private readonly List<Food> _newFood = [];
    private readonly List<Oasis> _oases = [];
    private readonly Queue<HistorySample> _history = new();
    private readonly SpatialGrid _creatureGrid;
    private readonly SpatialGrid _foodGrid;
    private readonly List<int> _scratch = [];
    private float[] _xs = [];
    private float[] _ys = [];
    private int _nextId = 1;
    private int _plantCount;

    public World(SimulationSettings? settings = null)
    {
        Settings = settings ?? new SimulationSettings();
        _rng = new Random(Settings.Seed);
        _creatureGrid = new SpatialGrid(Settings.Width, Settings.Height, CellSize);
        _foodGrid = new SpatialGrid(Settings.Width, Settings.Height, CellSize);

        CreateOases();
        for (int i = 0; i < Settings.InitialPlants; i++)
            SpawnPlant();
        for (int i = 0; i < Settings.InitialCreatures; i++)
            SpawnRandomCreature();
    }

    public SimulationSettings Settings { get; }
    public int Tick { get; private set; }
    public IReadOnlyList<Creature> Creatures => _creatures;
    public IReadOnlyList<Food> Food => _food;
    public IReadOnlyList<Oasis> Oases => _oases;
    public IReadOnlyCollection<HistorySample> History => _history;
    public WorldCounters Counters { get; } = new();

    private static List<int> Buffer => t_buffer ??= new List<int>(256);

    public void Step()
    {
        Tick++;
        BuildGrids();
        SenseAndThink();

        foreach (var creature in _creatures)
        {
            if (!creature.IsDead)
                Act(creature);
        }

        AgeMeat();
        RemoveDeadAndEaten();
        UpdateOases();
        GrowPlants();
        KeepMinimumPopulation();

        if (Tick % Settings.HistoryInterval == 0)
            RecordHistory();
    }

    public Creature? FindNearest(float x, float y, float maxDistance)
    {
        Creature? best = null;
        float bestDistance = maxDistance * maxDistance;
        foreach (var c in _creatures)
        {
            float d2 = DistanceSquared(x, y, c.X, c.Y);
            if (d2 <= bestDistance)
            {
                best = c;
                bestDistance = d2;
            }
        }
        return best;
    }

    public Creature? FindById(int id) => _creatures.Find(c => c.Id == id);

    public WorldStats ComputeStats()
    {
        int herbivores = 0, omnivores = 0, carnivores = 0, maxGeneration = 0, meat = 0;
        double generation = 0, size = 0, speed = 0, vision = 0, fov = 0, diet = 0, mutation = 0;

        foreach (var c in _creatures)
        {
            switch (c.DietClass)
            {
                case DietClass.Herbivore: herbivores++; break;
                case DietClass.Omnivore: omnivores++; break;
                default: carnivores++; break;
            }

            var g = c.Genome;
            generation += c.Generation;
            maxGeneration = Math.Max(maxGeneration, c.Generation);
            size += g.Size;
            speed += g.Speed;
            vision += g.Vision;
            fov += g.FieldOfView;
            diet += g.Diet;
            mutation += g.MutationRate;
        }

        foreach (var f in _food)
        {
            if (f.Kind == FoodKind.Meat)
                meat++;
        }

        int n = Math.Max(1, _creatures.Count);
        return new WorldStats(
            Tick, _creatures.Count, _plantCount, meat,
            herbivores, omnivores, carnivores,
            generation / n, maxGeneration,
            size / n, speed / n, vision / n, fov / n, diet / n, mutation / n,
            Counters.Births, Counters.Deaths, Counters.Kills, Counters.Immigrants);
    }

    private void BuildGrids()
    {
        EnsureCapacity(Math.Max(_creatures.Count, _food.Count));

        for (int i = 0; i < _creatures.Count; i++)
        {
            _xs[i] = _creatures[i].X;
            _ys[i] = _creatures[i].Y;
        }
        _creatureGrid.Build(_xs.AsSpan(0, _creatures.Count), _ys.AsSpan(0, _creatures.Count));

        var food = CollectionsMarshal.AsSpan(_food);
        for (int i = 0; i < food.Length; i++)
        {
            _xs[i] = food[i].X;
            _ys[i] = food[i].Y;
        }
        _foodGrid.Build(_xs.AsSpan(0, food.Length), _ys.AsSpan(0, food.Length));
    }

    private void EnsureCapacity(int count)
    {
        if (_xs.Length >= count)
            return;
        _xs = new float[count * 2];
        _ys = new float[count * 2];
    }

    private void SenseAndThink()
    {
        // Каждое существо пишет только в свои поля, поэтому параллельный проход детерминирован.
        if (_creatures.Count < ParallelThreshold)
        {
            foreach (var c in _creatures)
            {
                Sense(c, Buffer);
                c.Brain.Think();
            }
            return;
        }

        Parallel.For(0, _creatures.Count, i =>
        {
            var c = _creatures[i];
            Sense(c, Buffer);
            c.Brain.Think();
        });
    }

    private void Sense(Creature c, List<int> buffer)
    {
        var input = c.Brain.Inputs;
        Array.Clear(input, 0, Brain.InEnergy);

        var g = c.Genome;
        float range = g.Vision;
        float range2 = range * range;
        float halfFov = g.FieldOfView * 0.5f;
        float sectorScale = Brain.Sectors / g.FieldOfView;

        var food = CollectionsMarshal.AsSpan(_food);
        _foodGrid.Query(c.X, c.Y, range, buffer);
        foreach (int i in buffer)
        {
            ref readonly var f = ref food[i];
            float dx = Torus.Delta(c.X, f.X, Settings.Width);
            float dy = Torus.Delta(c.Y, f.Y, Settings.Height);
            float d2 = dx * dx + dy * dy;
            if (d2 > range2)
                continue;

            float relative = Torus.WrapAngle(MathF.Atan2(dy, dx) - c.Angle);
            if (relative < -halfFov || relative > halfFov)
                continue;

            int sector = Math.Min(Brain.Sectors - 1, (int)((relative + halfFov) * sectorScale));
            int slot = (f.Kind == FoodKind.Plant ? Brain.InPlant : Brain.InMeat) + sector;
            float closeness = 1f - MathF.Sqrt(d2) / range;
            if (closeness > input[slot])
                input[slot] = closeness;
        }

        bool touching = false;
        _creatureGrid.Query(c.X, c.Y, MathF.Max(range, c.Radius + Creature.MaxRadius), buffer);
        foreach (int i in buffer)
        {
            var other = _creatures[i];
            if (ReferenceEquals(other, c))
                continue;

            float dx = Torus.Delta(c.X, other.X, Settings.Width);
            float dy = Torus.Delta(c.Y, other.Y, Settings.Height);
            float d2 = dx * dx + dy * dy;
            float touch = c.Radius + other.Radius;
            if (d2 < touch * touch)
                touching = true;
            if (d2 > range2)
                continue;

            float relative = Torus.WrapAngle(MathF.Atan2(dy, dx) - c.Angle);
            if (relative < -halfFov || relative > halfFov)
                continue;

            int sector = Math.Min(Brain.Sectors - 1, (int)((relative + halfFov) * sectorScale));
            float closeness = 1f - MathF.Sqrt(d2) / range;
            if (closeness > input[Brain.InCreature + sector])
            {
                input[Brain.InCreature + sector] = closeness;
                input[Brain.InOtherness + sector] = HueDistance(g.Hue, other.Genome.Hue);
            }
        }

        input[Brain.InEnergy] = c.Energy / c.MaxEnergy;
        input[Brain.InSpeed] = c.Speed / c.MaxSpeed;
        input[Brain.InAge] = (float)c.Age / c.Lifespan;
        input[Brain.InClock] = MathF.Sin(c.ClockPhase);
        input[Brain.InTouch] = touching ? 1f : 0f;
        input[Brain.InPain] = c.Pain;
        c.Pain = 0;
        for (int m = 0; m < Brain.MemoryCount; m++)
            input[Brain.InMemory + m] = c.Brain.Outputs[Brain.OutMemory + m];
    }

    private void Act(Creature c)
    {
        var s = Settings;
        var g = c.Genome;
        var output = c.Brain.Outputs;

        // Движение: поворот, разгон к желаемой скорости, назад — медленнее.
        c.Angle = Torus.WrapAngle(c.Angle + output[Brain.OutTurn] * s.MaxTurnRate);
        float thrust = output[Brain.OutThrust];
        float targetSpeed = c.MaxSpeed * (thrust >= 0 ? thrust : thrust * ReverseThrust);
        c.Speed += (targetSpeed - c.Speed) * Acceleration;
        c.X = Torus.Wrap(c.X + MathF.Cos(c.Angle) * c.Speed, s.Width);
        c.Y = Torus.Wrap(c.Y + MathF.Sin(c.Angle) * c.Speed, s.Height);

        c.ClockPhase += g.ClockRate;
        if (c.ClockPhase > MathF.Tau)
            c.ClockPhase -= MathF.Tau;
        c.Age++;
        if (c.ReproductionCooldown > 0)
            c.ReproductionCooldown--;

        // Обмен веществ: тело, движение, зрение.
        float relativeSpeed = c.Speed / Creature.BaseSpeed;
        c.Energy -= g.Size * (s.BaseMetabolism + s.MoveCost * relativeSpeed * relativeSpeed)
                    + s.VisionCost * g.Vision
                    + s.FieldOfViewCost * g.FieldOfView;

        Eat(c);

        float attack = output[Brain.OutAttack];
        c.Biting = false;
        if (attack > 0)
        {
            c.Energy -= s.AttackCost * g.Size * attack;
            Bite(c, attack);
        }

        if (output[Brain.OutReproduce] > 0)
            TryReproduce(c);

        if (c.Energy <= 0 || c.Age >= c.Lifespan)
            Die(c);
    }

    private void Eat(Creature c)
    {
        if (c.Energy >= c.MaxEnergy)
            return;

        float plantDigestion = 1f - c.Genome.Diet;
        float meatDigestion = c.Genome.Diet;
        float reach = c.Radius + global::AL.Core.Food.Radius;
        float reach2 = reach * reach;

        var food = CollectionsMarshal.AsSpan(_food);
        _foodGrid.Query(c.X, c.Y, reach, _scratch);
        foreach (int i in _scratch)
        {
            ref var f = ref food[i];
            if (f.Eaten)
                continue;

            float digestion = f.Kind == FoodKind.Plant ? plantDigestion : meatDigestion;
            if (digestion < Settings.MinDigestion)
                continue;
            if (DistanceSquared(c.X, c.Y, f.X, f.Y) > reach2)
                continue;

            f.Eaten = true;
            float gain = f.Energy * digestion;
            c.Energy = MathF.Min(c.MaxEnergy, c.Energy + gain);
            if (f.Kind == FoodKind.Plant)
            {
                _plantCount--;
                c.PlantEnergyEaten += gain;
            }
            else
            {
                c.MeatEnergyEaten += gain;
            }

            if (c.Energy >= c.MaxEnergy)
                break;
        }
    }

    private void Bite(Creature c, float strength)
    {
        _creatureGrid.Query(c.X, c.Y, c.Radius + Creature.MaxRadius + MovementMargin, _scratch);

        Creature? victim = null;
        float best = float.MaxValue;
        foreach (int i in _scratch)
        {
            var other = _creatures[i];
            if (ReferenceEquals(other, c) || other.IsDead)
                continue;

            float dx = Torus.Delta(c.X, other.X, Settings.Width);
            float dy = Torus.Delta(c.Y, other.Y, Settings.Height);
            float d2 = dx * dx + dy * dy;
            float touch = c.Radius + other.Radius;
            if (d2 > touch * touch || d2 >= best)
                continue;

            // Кусать можно только то, что перед собой.
            if (MathF.Abs(Torus.WrapAngle(MathF.Atan2(dy, dx) - c.Angle)) > MathF.PI / 2)
                continue;

            victim = other;
            best = d2;
        }

        if (victim is null)
            return;

        c.Biting = true;
        float damage = MathF.Min(Settings.BiteDamage * c.Genome.Size * strength, MathF.Max(victim.Energy, 0));
        victim.Energy -= damage;
        victim.Pain = MathF.Min(1f, victim.Pain + damage / 4f);

        float gain = damage * c.Genome.Diet;
        c.Energy = MathF.Min(c.MaxEnergy, c.Energy + gain);
        c.MeatEnergyEaten += gain;

        if (victim.Energy <= 0)
        {
            c.Kills++;
            Counters.Kills++;
            Die(victim);
        }
    }

    private void TryReproduce(Creature parent)
    {
        var s = Settings;
        if (parent.ReproductionCooldown > 0 || parent.Age < s.MaturityAge)
            return;
        if (_creatures.Count + _newborns.Count >= s.MaxCreatures)
            return;
        if (parent.Energy < parent.Genome.ReproductionThreshold * parent.MaxEnergy)
            return;

        var genome = parent.Genome.Mutate(_rng);
        float childEnergy = parent.Energy * parent.Genome.ChildShare;
        float cost = childEnergy + Creature.BodyEnergyFor(genome);
        if (parent.Energy - cost < 0.05f * parent.MaxEnergy)
            return;

        parent.Energy -= cost;
        parent.Children++;
        parent.ReproductionCooldown = s.ReproductionCooldown;

        var child = new Creature(_nextId++, genome, parent.Generation + 1, parent.Id, RandomLifespan(), Tick);
        float behind = parent.Radius + child.Radius;
        child.X = Torus.Wrap(parent.X - MathF.Cos(parent.Angle) * behind, s.Width);
        child.Y = Torus.Wrap(parent.Y - MathF.Sin(parent.Angle) * behind, s.Height);
        child.Angle = Torus.WrapAngle(parent.Angle + _rng.NextFloat(-MathF.PI, MathF.PI));
        child.Energy = MathF.Min(childEnergy, child.MaxEnergy);
        child.ReproductionCooldown = s.ReproductionCooldown;

        _newborns.Add(child);
        Counters.Births++;
    }

    private void Die(Creature c)
    {
        if (c.IsDead)
            return;

        c.IsDead = true;
        c.DeathTick = Tick;
        Counters.Deaths++;

        // Тело становится мясом: энергия тела плюс половина оставшегося запаса.
        float meat = c.BodyEnergy + MathF.Max(0, c.Energy) * 0.5f;
        int chunks = Math.Max(1, (int)MathF.Ceiling(meat / Settings.MeatChunkEnergy));
        for (int i = 0; i < chunks; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float distance = _rng.NextSingle() * c.Radius;
            _newFood.Add(new Food(
                Torus.Wrap(c.X + MathF.Cos(angle) * distance, Settings.Width),
                Torus.Wrap(c.Y + MathF.Sin(angle) * distance, Settings.Height),
                meat / chunks,
                FoodKind.Meat));
        }
    }

    private void AgeMeat()
    {
        foreach (ref var f in CollectionsMarshal.AsSpan(_food))
        {
            if (f.Kind == FoodKind.Meat && ++f.Age > Settings.MeatLifetime)
                f.Eaten = true;
        }
    }

    private void RemoveDeadAndEaten()
    {
        _creatures.RemoveAll(c => c.IsDead);
        _creatures.AddRange(_newborns);
        _newborns.Clear();

        _food.RemoveAll(f => f.Eaten);
        _food.AddRange(_newFood);
        _newFood.Clear();
    }

    private void CreateOases()
    {
        var s = Settings;
        for (int i = 0; i < s.OasisCount; i++)
        {
            float direction = _rng.NextSingle() * MathF.Tau;
            _oases.Add(new Oasis
            {
                X = _rng.NextSingle() * s.Width,
                Y = _rng.NextSingle() * s.Height,
                Radius = _rng.NextFloat(120f, 220f),
                VelocityX = MathF.Cos(direction) * s.OasisDrift,
                VelocityY = MathF.Sin(direction) * s.OasisDrift,
                SeasonPhase = _rng.NextSingle() * MathF.Tau,
            });
        }
        UpdateOases();
    }

    private void UpdateOases()
    {
        var s = Settings;
        float season = MathF.Tau * Tick / s.SeasonLength;
        foreach (var o in _oases)
        {
            o.X = Torus.Wrap(o.X + o.VelocityX, s.Width);
            o.Y = Torus.Wrap(o.Y + o.VelocityY, s.Height);
            o.Fertility = MathF.Max(0f, 1f + s.SeasonStrength * MathF.Sin(season + o.SeasonPhase));
        }
    }

    private void GrowPlants()
    {
        float expected = Settings.PlantGrowth;
        int count = (int)expected + (_rng.NextSingle() < expected - (int)expected ? 1 : 0);
        for (int i = 0; i < count && _plantCount < Settings.MaxPlants; i++)
            SpawnPlant();
    }

    private void SpawnPlant()
    {
        var s = Settings;
        float x, y;
        var oasis = _rng.NextSingle() < s.OasisShare ? PickOasis() : null;
        if (oasis is not null)
        {
            x = Torus.Wrap(oasis.X + _rng.NextGaussian() * oasis.Radius * 0.5f, s.Width);
            y = Torus.Wrap(oasis.Y + _rng.NextGaussian() * oasis.Radius * 0.5f, s.Height);
        }
        else
        {
            x = _rng.NextSingle() * s.Width;
            y = _rng.NextSingle() * s.Height;
        }

        _food.Add(new Food(x, y, s.PlantEnergy, FoodKind.Plant));
        _plantCount++;
    }

    /// <summary>Случайный оазис с вероятностью, пропорциональной его текущему плодородию.</summary>
    private Oasis? PickOasis()
    {
        float total = 0;
        foreach (var o in _oases)
            total += o.Fertility;
        if (total <= 0)
            return null;

        float pick = _rng.NextSingle() * total;
        foreach (var o in _oases)
        {
            pick -= o.Fertility;
            if (pick <= 0)
                return o;
        }
        return _oases[^1];
    }

    private void KeepMinimumPopulation()
    {
        while (_creatures.Count < Settings.MinCreatures)
        {
            SpawnRandomCreature();
            Counters.Immigrants++;
        }
    }

    /// <summary>Добавляет существо с заданным геномом (поколение 0) в случайное место мира.</summary>
    public Creature Spawn(Genome genome)
    {
        var c = new Creature(_nextId++, genome, 0, 0, RandomLifespan(), Tick)
        {
            X = _rng.NextSingle() * Settings.Width,
            Y = _rng.NextSingle() * Settings.Height,
            Angle = _rng.NextFloat(-MathF.PI, MathF.PI),
        };
        c.Energy = c.MaxEnergy * 0.7f;
        _creatures.Add(c);
        return c;
    }

    private void SpawnRandomCreature() => Spawn(Genome.CreateRandom(_rng));

    internal void AddFood(Food food)
    {
        _food.Add(food);
        if (food.Kind == FoodKind.Plant)
            _plantCount++;
    }

    private int RandomLifespan() => (int)(Settings.Lifespan * _rng.NextFloat(0.85f, 1.15f));

    private void RecordHistory()
    {
        int herbivores = 0, omnivores = 0, carnivores = 0, meat = 0;
        foreach (var c in _creatures)
        {
            switch (c.DietClass)
            {
                case DietClass.Herbivore: herbivores++; break;
                case DietClass.Omnivore: omnivores++; break;
                default: carnivores++; break;
            }
        }
        foreach (var f in _food)
        {
            if (f.Kind == FoodKind.Meat)
                meat++;
        }

        _history.Enqueue(new HistorySample(Tick, herbivores, omnivores, carnivores, _plantCount, meat));
        while (_history.Count > Settings.HistoryCapacity)
            _history.Dequeue();
    }

    private float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        float dx = Torus.Delta(x1, x2, Settings.Width);
        float dy = Torus.Delta(y1, y2, Settings.Height);
        return dx * dx + dy * dy;
    }

    private static float HueDistance(float a, float b)
    {
        float d = MathF.Abs(a - b);
        return MathF.Min(d, 1f - d) * 2f;
    }
}
