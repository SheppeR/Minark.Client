using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Minark.Server.Data;
using Minark.Server.Http;
using Minark.Server.Infrastructure;
using Minark.Server.Networking;
using Minark.Server.Networking.Handlers.Chat;
using Minark.Server.Networking.Handlers.Profile;
using Minark.Server.Services;
using Minark.Server.Services.Interfaces;
using Minark.Server.Startup;
using Minark.Shared;
using Serilog;

Log.Logger = SerilogUtils.SetupServer();

var sw = Stopwatch.StartNew();

try
{
    SerilogUtils.PrintSection("MINARK SERVER");
    Log.Information("Starting server...");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSingleton(sw);
    builder.Services.AddSerilog(Log.Logger);
    builder.Logging.ClearProviders();

    var connectionString = builder.Configuration.GetConnectionString("MariaDb")
                           ?? throw new InvalidOperationException("MariaDb connection string is missing.");

    // ── Database ──────────────────────────────────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
            mysqlOptions => mysqlOptions.MigrationsAssembly("Minark.Server")));

    // ── Memory caching ────────────────────────────────────────────────────────
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ITokenCacheService, TokenCacheService>();

    // ── Réseau WatsonTcp ──────────────────────────────────────────────────────
    builder.Services.AddSingleton<TcpServerFactory>();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<TcpServerFactory>().Create());

    // ── Services ──────────────────────────────────────────────────────────────
    builder.Services.AddSingleton<ISessionStore, SessionStore>();
    builder.Services.AddSingleton<IChallengeStore, ChallengeStore>();
    builder.Services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();
    builder.Services.AddSingleton<IClientBroadcaster, ClientBroadcaster>();
    builder.Services.AddScoped<DatabaseInitializer>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IFriendService, FriendService>();
    builder.Services.AddScoped<INewsService, NewsService>();
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<IProfileService, ProfileService>();

    // ── Packet routing ────────────────────────────────────────────────────────
    builder.Services.AddSingleton<IServerSender, ServerSender>();
    builder.Services.AddSingleton<PacketRouter>();

    // ── Auto-enregistrement des IPacketHandler via Scrutor ────────────────────
    builder.Services.Scan(scan => scan
        .FromAssemblyOf<IPacketHandler>()
        .AddClasses(classes => classes
            .AssignableTo<IPacketHandler>()
            .Where(t => t != typeof(TypingHandler) && t != typeof(BlockHandler)))
        .As<IPacketHandler>()
        .WithScopedLifetime());

    // Typing : même classe, deux PacketType (TypingStart / TypingStop)
    builder.Services.AddScoped<IPacketHandler>(sp =>
        new TypingHandler(sp.GetRequiredService<ISessionStore>(),
            sp.GetRequiredService<IServerSender>(), PacketType.TypingStart));
    builder.Services.AddScoped<IPacketHandler>(sp =>
        new TypingHandler(sp.GetRequiredService<ISessionStore>(),
            sp.GetRequiredService<IServerSender>(), PacketType.TypingStop));

    // Block : même classe, BlockUser et UnblockUser via le flag `block`
    builder.Services.AddScoped<IPacketHandler>(sp =>
        new BlockHandler(sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IProfileService>(),
            sp.GetRequiredService<IServerSender>(), true));
    builder.Services.AddScoped<IPacketHandler>(sp =>
        new BlockHandler(sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IProfileService>(),
            sp.GetRequiredService<IServerSender>(), false));

    // ── Orchestrateur de démarrage ────────────────────────────────────────────
    builder.Services.AddSingleton<ServerReadySignal>();

    // Étapes enregistrées en tant que IStartupStep (ordre contrôlé par Step.Order)
    builder.Services.AddSingleton<IStartupStep, DatabaseStartupStep>();
    builder.Services.AddSingleton<IStartupStep, NetworkStartupStep>();
    // ↑ Pour ajouter une étape future : implémenter IStartupStep et l'enregistrer ici.

    // L'orchestrateur DOIT être le PREMIER hosted service
    builder.Services.AddHostedService<ServerOrchestrator>();

    // ── Services hébergés (démarrent après l'orchestrateur via ServerReadySignal) ──
    builder.Services.AddHostedService<MessagePurgeService>();

    // ── HTTP interne (GameServer → Server) ────────────────────────────────────
    // Écoute uniquement sur 127.0.0.1 — jamais exposé à l'extérieur.
    builder.Services.AddHostedService<InternalHttpService>();

    var app = builder.Build();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}