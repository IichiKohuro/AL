using System.Net.WebSockets;
using System.Text.Json.Serialization;
using AL.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SimulationService>();
builder.Services.AddHostedService(services => services.GetRequiredService<SimulationService>());
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals);

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

var api = app.MapGroup("/api");

api.MapGet("/stats", (SimulationService sim) => sim.GetStatsAsync());

api.MapGet("/species", (SimulationService sim) => sim.GetSpeciesAsync());

api.MapPost("/control", (ControlRequest request, SimulationService sim) =>
{
    if (request.Speed is { } speed && !SimulationService.Speeds.Contains(speed))
        return Results.BadRequest($"Скорость должна быть одной из: {string.Join(", ", SimulationService.Speeds)}.");

    if (request.Speed is { } newSpeed)
        sim.SetSpeed(newSpeed);
    if (request.Paused is { } paused)
        sim.SetPaused(paused);
    return Results.Ok(sim.State);
});

api.MapPost("/reset", async (ResetRequest? request, SimulationService sim) =>
{
    await sim.ResetAsync(request?.Seed);
    return Results.Ok(sim.State);
});

api.MapPost("/select", async (SelectRequest request, SimulationService sim) =>
    await sim.SelectAsync(request.X, request.Y) is { } creature ? Results.Ok(creature) : Results.NoContent());

api.MapDelete("/select", async (SimulationService sim) =>
{
    await sim.ClearSelectionAsync();
    return Results.NoContent();
});

api.MapGet("/creatures/{id:int}", async (int id, SimulationService sim) =>
    await sim.GetCreatureAsync(id) is { } creature ? Results.Ok(creature) : Results.NotFound());

// Поток кадров: сервер шлёт бинарный кадр, как только он готов; клиент ничего не отправляет.
app.Map("/ws", async (HttpContext context, SimulationService sim) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var connection = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    var receiving = ReceiveUntilClosedAsync(socket, connection);

    try
    {
        long version = 0;
        while (socket.State == WebSocketState.Open)
        {
            var frame = await sim.NextFrameAsync(version, connection.Token);
            version = frame.Version;
            await socket.SendAsync(frame.Data, WebSocketMessageType.Binary, endOfMessage: true, connection.Token);
        }
    }
    catch (Exception e) when (e is OperationCanceledException or WebSocketException)
    {
        // Клиент ушёл.
    }
    finally
    {
        await connection.CancelAsync();
        await receiving;
        if (socket.State == WebSocketState.CloseReceived)
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }
});

app.Run();

static async Task ReceiveUntilClosedAsync(WebSocket socket, CancellationTokenSource connection)
{
    var buffer = new byte[1024];
    try
    {
        while (!connection.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, connection.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }
    catch (Exception e) when (e is OperationCanceledException or WebSocketException)
    {
    }
    finally
    {
        await connection.CancelAsync();
    }
}
