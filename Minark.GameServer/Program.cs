using System.Diagnostics;
using LiteNetLib;
using Microsoft.EntityFrameworkCore;
using Minark.GameServer;
using Minark.GameServer.Data;
using Minark.GameServer.Handlers;
using Minark.GameServer.Network;
using Minark.GameServer.Services;
using Minark.GameServer.Utils;
using Serilog;

Log.Logger = SerilogUtils.SetupServer();

var sw = Stopwatch.StartNew();

try
{
    SerilogUtils.PrintSection("MINARK GAME SERVER");
    Log.Information("Starting server...");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog(Log.Logger);
    builder.Logging.ClearProviders();

    // ── Config ────────────────────────────────────────────────────────────────
    var gsOpts = builder.Configuration
        .GetSection(GameServerOptions.Section)
        .Get<GameServerOptions>() ?? new GameServerOptions();
    builder.Services.AddSingleton(gsOpts);

    // ── Database ──────────────────────────────────────────────────────────────
    builder.Services.AddDbContextFactory<GameDbContext>(o =>
        o.UseMySql(
            builder.Configuration.GetConnectionString("MariaDb"),
            ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("MariaDb"))));

    // ── Réseau LiteNetLib ─────────────────────────────────────────────────────
    var listener = new EventBasedNetListener();
    var netManager = new NetManager(listener) { AutoRecycle = true };
    builder.Services.AddSingleton(listener);
    builder.Services.AddSingleton(netManager);

    // ── Services ──────────────────────────────────────────────────────────────
    builder.Services.AddSingleton<PlayerRegistry>();
    builder.Services.AddSingleton<IServerSender, ServerSender>();
    builder.Services.AddSingleton<PacketDispatcher>();

    // ── Handlers (auto-enregistrement via Scrutor) ────────────────────────────
    builder.Services.Scan(scan => scan
        .FromAssemblyOf<AuthHandler>()
        .AddClasses(c => c.AssignableTo<IPacketHandler>())
        .AsImplementedInterfaces()
        .AsSelf()
        .WithSingletonLifetime());

    // ── Hosted services ───────────────────────────────────────────────────────
    builder.Services.AddHostedService<LiteNetHostedService>();
    builder.Services.AddHostedService<TickService>();

    var app = builder.Build();

    Log.Information("Démarrage en {ElapsedMs}ms", sw.ElapsedMilliseconds);
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GameServer terminé de façon inattendue");
}
finally
{
    await Log.CloseAndFlushAsync();
}