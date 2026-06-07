using System.Net.Http.Json;

namespace Minark.GameServer.Services;

public sealed class MinarServerStatusClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MinarServerStatusClient> _log;

    public MinarServerStatusClient(IConfiguration config, ILogger<MinarServerStatusClient> log)
    {
        _log = log;

        var host = config["Internal:MinarServerHost"] ?? "145.239.206.41";
        var port = config.GetValue("Internal:MinarServerPort", 9001);
        var sharedKey = config["Internal:SharedKey"] ?? "spokspok03";

        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://{host}:{port}"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        _http.DefaultRequestHeaders.Add("X-Internal-Key", sharedKey);
    }

    public Task SetInGameAsync(string token)
    {
        return PushStatusAsync(token, 4);
        // 4 = InGame
    }

    public Task RestoreStatusAsync(string token, int previousStatusInt)
    {
        return PushStatusAsync(token, previousStatusInt);
    }

    private async Task PushStatusAsync(string token, int status)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("/internal/player-status",
                new { Token = token, Status = status });

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("MinarServerStatusClient: réponse {Code} pour le statut {Status}",
                    (int)response.StatusCode, status);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MinarServerStatusClient: impossible de notifier le statut {Status}", status);
        }
    }
}