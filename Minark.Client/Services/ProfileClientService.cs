using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Minark.Client.Networking;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;
using Minark.Shared.Packets.Auth;
using Minark.Shared.Packets.Friends;

namespace Minark.Client.Services;

public class ProfileClientService(TcpClientService tcp, PacketDispatcher dispatcher) : IProfileClientService
{
    private static readonly HttpClient _http = new();

    public Task<AckResponse> ChangePasswordAsync(string token, string oldPassword, string newPassword)
    {
        return RequestAsync<AckResponse>(
            PacketType.ChangePasswordRequest, PacketType.ChangePasswordResponse,
            new ChangePasswordRequest { Token = token, OldPasswordHash = oldPassword, NewPasswordHash = newPassword });
    }

    public async Task<UpdateAvatarResponse> UpdateAvatarAsync(string token, string filePath)
    {
        var uploadResp = await UploadAvatarAsync(token, filePath);
        if (!uploadResp.Success || string.IsNullOrWhiteSpace(uploadResp.AvatarUrl))
        {
            return uploadResp;
        }

        var syncResp = await RequestAsync<UpdateAvatarResponse>(
            PacketType.UpdateAvatarRequest, PacketType.UpdateAvatarResponse,
            new UpdateAvatarRequest { Token = token, AvatarUrl = uploadResp.AvatarUrl });

        return syncResp.Success ? uploadResp : syncResp;
    }

    public Task<AckResponse> BlockUserAsync(string token, string targetUsername)
    {
        return RequestAsync<AckResponse>(PacketType.BlockUser, PacketType.BlockResponse,
            new BlockUserRequest { Token = token, TargetUsername = targetUsername });
    }

    public Task<AckResponse> UnblockUserAsync(string token, string targetUsername)
    {
        return RequestAsync<AckResponse>(PacketType.UnblockUser, PacketType.BlockResponse,
            new BlockUserRequest { Token = token, TargetUsername = targetUsername });
    }

    public Task<BlockListResponse> GetBlockListAsync(string token)
    {
        return RequestAsync<BlockListResponse>(PacketType.BlockListRequest, PacketType.BlockListResponse,
            new TokenRequest { Token = token });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    public async Task<UpdateAvatarResponse> UploadAvatarAsync(string token, string filePath)
    {
        try
        {
            var url = $"{WebConfig.BaseUrl}/api/avatar/upload";
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(filePath));
            form.Add(fileContent, "file", Path.GetFileName(filePath));

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = form;

            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                string? errMsg = null;
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(json);
                    errMsg = err.TryGetProperty("error", out var e) ? e.GetString() : null;
                }
                catch
                {
                    /* ignored */
                }

                return new UpdateAvatarResponse
                    { Success = false, ErrorMessage = errMsg ?? $"HTTP {(int)resp.StatusCode}" };
            }

            var result = JsonSerializer.Deserialize<JsonElement>(json);
            return new UpdateAvatarResponse
                { Success = true, AvatarUrl = result.GetProperty("avatar_url").GetString() ?? string.Empty };
        }
        catch (Exception ex)
        {
            return new UpdateAvatarResponse { Success = false, ErrorMessage = $"Erreur réseau : {ex.Message}" };
        }
    }

    private async Task<T> RequestAsync<T>(PacketType sendType, PacketType recvType, object payload) where T : new()
    {
        var tcs = new TaskCompletionSource<T>();

        void H(string p)
        {
            tcs.TrySetResult(PacketSerializer.DeserializePayload<T>(p) ?? new T());
        }

        dispatcher.Register(recvType, H);
        await tcp.SendAsync(sendType, payload);
        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            dispatcher.Unregister(recvType, H);
        }
    }

    private static string GetMimeType(string path)
    {
        return Path.GetExtension(path).ToLower() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}