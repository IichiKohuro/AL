using System.Collections.Concurrent;
using System.Diagnostics;
using AL.Core;

namespace AL.Web;

/// <summary>
/// Крутит симуляцию в отдельном потоке и публикует кадры для WebSocket-клиентов.
/// Мир не потокобезопасен, поэтому все обращения к нему идут через очередь команд
/// и выполняются в потоке симуляции между тиками.
/// </summary>
public sealed class SimulationService(ILogger<SimulationService> logger, IConfiguration configuration) : BackgroundService
{
    public const int FramesPerSecond = 30;

    /// <summary>Тиков на кадр. 0 — считать столько, сколько успеваем.</summary>
    public static readonly int[] Speeds = [1, 4, 16, 0];

    private const float SelectionRadius = 30f;
    private const int HistoryPoints = 300;

    private readonly ConcurrentQueue<Action> _commands = new();
    // Сид можно задать при запуске: dotnet run -- --seed 42
    private World _world = CreateWorld(configuration.GetValue<int?>("Seed"));
    private Creature? _selected;
    private volatile Frame _frame = new(0, []);
    private TaskCompletionSource _frameSignal = NewSignal();
    private volatile bool _paused;
    private volatile int _speed = 1;
    private volatile float _ticksPerSecond;

    public SimulationState State => new(_paused, _speed, _world.Tick, _ticksPerSecond, _world.Settings.Seed, _selected?.Id);

    public void SetPaused(bool paused) => _paused = paused;

    public void SetSpeed(int speed)
    {
        if (!Speeds.Contains(speed))
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Допустимые скорости: 1, 4, 16, 0 (максимум).");
        _speed = speed;
    }

    public Task ResetAsync(int? seed) => InvokeAsync(_ =>
    {
        _world = CreateWorld(seed);
        _selected = null;
        logger.LogInformation("Новый мир, сид {Seed}", _world.Settings.Seed);
        return true;
    });

    public Task<CreatureDetails?> SelectAsync(float x, float y) => InvokeAsync(world =>
    {
        _selected = world.FindNearest(x, y, SelectionRadius);
        return _selected is null ? null : CreatureDetails.From(_selected);
    });

    public Task ClearSelectionAsync() => InvokeAsync(_ => _selected = null);

    /// <summary>Выбранное существо остаётся доступным и после смерти — чтобы было видно, чем всё кончилось.</summary>
    public Task<CreatureDetails?> GetCreatureAsync(int id) => InvokeAsync(world =>
    {
        var creature = _selected?.Id == id ? _selected : world.FindById(id);
        return creature is null ? null : CreatureDetails.From(creature);
    });

    public Task<StatsResponse> GetStatsAsync() =>
        InvokeAsync(world => new StatsResponse(State, world.ComputeStats(), Downsample(world.History, HistoryPoints)));

    /// <summary>Ждёт кадр новее <paramref name="lastVersion"/>. Медленный клиент просто пропускает промежуточные кадры.</summary>
    public async Task<Frame> NextFrameAsync(long lastVersion, CancellationToken cancellationToken)
    {
        while (true)
        {
            // Сначала берём сигнал, потом кадр: публикация между ними не потеряется.
            var signal = Volatile.Read(ref _frameSignal);
            var frame = _frame;
            if (frame.Version != lastVersion)
                return frame;
            await signal.Task.WaitAsync(cancellationToken);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => Run(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private void Run(CancellationToken stoppingToken)
    {
        var frameTime = TimeSpan.FromSeconds(1.0 / FramesPerSecond);
        var clock = Stopwatch.StartNew();
        var rateClock = Stopwatch.StartNew();
        int ticks = 0;
        logger.LogInformation("Симуляция запущена, сид {Seed}", _world.Settings.Seed);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var frameStart = clock.Elapsed;
                bool changed = RunCommands();

                if (!_paused)
                {
                    if (_speed == 0)
                    {
                        var deadline = frameStart + frameTime * 0.9;
                        do
                        {
                            _world.Step();
                            ticks++;
                        }
                        while (clock.Elapsed < deadline);
                    }
                    else
                    {
                        for (int i = 0; i < _speed; i++)
                        {
                            _world.Step();
                            ticks++;
                        }
                    }
                    changed = true;
                }

                if (changed)
                    PublishFrame();

                if (rateClock.ElapsedMilliseconds >= 1000)
                {
                    _ticksPerSecond = (float)(ticks * 1000.0 / rateClock.ElapsedMilliseconds);
                    ticks = 0;
                    rateClock.Restart();
                }

                var rest = frameTime - (clock.Elapsed - frameStart);
                if (rest > TimeSpan.Zero)
                    Thread.Sleep(rest);
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Симуляция упала на тике {Tick}", _world.Tick);
            throw;
        }
    }

    private Task<T> InvokeAsync<T>(Func<World, T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() =>
        {
            try
            {
                completion.SetResult(action(_world));
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        });
        return completion.Task;
    }

    private bool RunCommands()
    {
        bool any = false;
        while (_commands.TryDequeue(out var command))
        {
            command();
            any = true;
        }
        return any;
    }

    private void PublishFrame()
    {
        _frame = new Frame(_frame.Version + 1, FrameEncoder.Encode(_world, _selected));
        Interlocked.Exchange(ref _frameSignal, NewSignal()).TrySetResult();
    }

    private static World CreateWorld(int? seed) =>
        new(new SimulationSettings { Seed = seed ?? Random.Shared.Next(1, 1_000_000) });

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static HistorySample[] Downsample(IReadOnlyCollection<HistorySample> history, int maxPoints)
    {
        var all = history.ToArray();
        if (all.Length <= maxPoints)
            return all;

        var result = new HistorySample[maxPoints];
        for (int i = 0; i < maxPoints; i++)
            result[i] = all[(int)((long)i * (all.Length - 1) / (maxPoints - 1))];
        return result;
    }
}

public sealed record Frame(long Version, byte[] Data);
