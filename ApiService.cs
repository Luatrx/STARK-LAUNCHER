using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StarkFNLauncher
{
    public static class Api
    {
        public static string BaseUrl { get; set; } = "https://stinking-marbled-cupcake.ngrok-free.dev";
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

        public static async Task<ApiResult<LoginResponse>> Login(string username, string password)
        {
            try
            {
                var res  = await _http.PostAsJsonAsync($"{BaseUrl}/auth/login", new { username, password });
                var json = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                    return ApiResult<LoginResponse>.Ok(JsonSerializer.Deserialize<LoginResponse>(json, _opts)!);
                var err = JsonSerializer.Deserialize<ErrResponse>(json, _opts);
                return ApiResult<LoginResponse>.Fail(err?.Error ?? "Login failed.");
            }
            catch (HttpRequestException) { return ApiResult<LoginResponse>.Fail("Cannot reach server. Is the backend running?"); }
            catch (TaskCanceledException)  { return ApiResult<LoginResponse>.Fail("Server timed out."); }
            catch (Exception ex)           { return ApiResult<LoginResponse>.Fail(ex.Message); }
        }

        public static async Task<ApiResult<LoginResponse>> LoginWithDiscord(DiscordUser d)
        {
            try
            {
                var res  = await _http.PostAsJsonAsync($"{BaseUrl}/auth/discord",
                    new { discordId = d.Id, discordTag = d.Tag, discordUsername = d.Username, accessToken = d.AccessToken, avatar = d.Avatar });
                var json = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                    return ApiResult<LoginResponse>.Ok(JsonSerializer.Deserialize<LoginResponse>(json, _opts)!);
                var err = JsonSerializer.Deserialize<ErrResponse>(json, _opts);
                return ApiResult<LoginResponse>.Fail(err?.Error ?? "Discord login failed.");
            }
            catch (HttpRequestException) { return ApiResult<LoginResponse>.Fail("Cannot reach server."); }
            catch (Exception ex)          { return ApiResult<LoginResponse>.Fail(ex.Message); }
        }

        public static async Task<ApiResult<RegisterResponse>> Register(string username, string password, string email = "")
        {
            try
            {
                var res  = await _http.PostAsJsonAsync($"{BaseUrl}/auth/register", new { username, password, email });
                var json = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                    return ApiResult<RegisterResponse>.Ok(JsonSerializer.Deserialize<RegisterResponse>(json, _opts)!);
                var err = JsonSerializer.Deserialize<ErrResponse>(json, _opts);
                return ApiResult<RegisterResponse>.Fail(err?.Error ?? "Register failed.");
            }
            catch (HttpRequestException) { return ApiResult<RegisterResponse>.Fail("Cannot reach server."); }
            catch (Exception ex)          { return ApiResult<RegisterResponse>.Fail(ex.Message); }
        }

        public static async Task<ApiResult<VerifyResponse>> Verify(string token)
        {
            try
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var res  = await _http.GetAsync($"{BaseUrl}/auth/verify");
                var json = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                    return ApiResult<VerifyResponse>.Ok(JsonSerializer.Deserialize<VerifyResponse>(json, _opts)!);
                return ApiResult<VerifyResponse>.Fail("Expired.");
            }
            catch { return ApiResult<VerifyResponse>.Fail("Could not verify."); }
        }

        public static async Task<ApiResult<ServerStatusResponse>> GetStatus()
        {
            try
            {
                var res  = await _http.GetAsync($"{BaseUrl}/server/status");
                var json = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                    return ApiResult<ServerStatusResponse>.Ok(JsonSerializer.Deserialize<ServerStatusResponse>(json, _opts)!);
                return ApiResult<ServerStatusResponse>.Fail("No status.");
            }
            catch { return ApiResult<ServerStatusResponse>.Fail("Unreachable."); }
        }

        public static async Task<ApiResult<StatsData>> GetStats(string token)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/stats");
                req.Headers.Add("Authorization", $"bearer {token}");
                var resp = await _http.SendAsync(req);
                var json = await resp.Content.ReadAsStringAsync();
                var doc  = JsonSerializer.Deserialize<JsonElement>(json);
                var d = new StatsData
                {
                    Wins     = doc.TryGetProperty("wins",     out var w)  ? w.GetInt32()  : 0,
                    Kills    = doc.TryGetProperty("kills",    out var k)  ? k.GetInt32()  : 0,
                    Deaths   = doc.TryGetProperty("deaths",   out var de) ? de.GetInt32() : 0,
                    Matches  = doc.TryGetProperty("matches",  out var m)  ? m.GetInt32()  : 0,
                    Vbucks   = doc.TryGetProperty("vbucks",   out var v)  ? v.GetInt32()  : 0,
                    Playtime = doc.TryGetProperty("playtime", out var p)  ? p.GetInt32()  : 0,
                };
                return ApiResult<StatsData>.Ok(d);
            }
            catch (Exception ex) { return ApiResult<StatsData>.Fail(ex.Message); }
        }

        public static async Task<ApiResult<System.Collections.Generic.List<FriendData>>> GetFriends(string token)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/friends");
                req.Headers.Add("Authorization", $"bearer {token}");
                var resp = await _http.SendAsync(req);
                var json = await resp.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<System.Collections.Generic.List<FriendData>>(json, _opts)
                           ?? new();
                return ApiResult<System.Collections.Generic.List<FriendData>>.Ok(list);
            }
            catch (Exception ex) { return ApiResult<System.Collections.Generic.List<FriendData>>.Fail(ex.Message); }
        }

        public static async Task<ApiResult<UpdateData>> CheckUpdate(string currentVersion)
        {
            try
            {
                var resp = await _http.GetAsync($"{BaseUrl}/api/launcher/version?current={currentVersion}");
                if (!resp.IsSuccessStatusCode) return ApiResult<UpdateData>.Fail("No update info");
                var json = await resp.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<UpdateData>(json, _opts)!;
                return ApiResult<UpdateData>.Ok(data);
            }
            catch { return ApiResult<UpdateData>.Fail("Could not check for updates."); }
        }

        public static void SetToken(string token) =>
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public class ApiResult<T>
    {
        public bool   Success { get; private set; }
        public T?     Data    { get; private set; }
        public string Error   { get; private set; } = "";
        public static ApiResult<T> Ok(T d)        => new() { Success = true,  Data = d };
        public static ApiResult<T> Fail(string e)  => new() { Success = false, Error = e };
    }

    public class StatsData
    {
        public int Wins     { get; set; }
        public int Kills    { get; set; }
        public int Deaths   { get; set; }
        public int Matches  { get; set; }
        public int Vbucks   { get; set; }
        public int Playtime { get; set; }
    }

    public class LoginResponse    { public string Token { get; set; } = ""; public UserInfo? User { get; set; } public bool IsNewAccount { get; set; } }
    public class RegisterResponse { public string Message { get; set; } = ""; }
    public class VerifyResponse   { public bool Valid { get; set; } public string Username { get; set; } = ""; public string Role { get; set; } = ""; }
    public class ErrResponse      { public string Error { get; set; } = ""; }
    public class UserInfo         { public string Username { get; set; } = ""; public string Role { get; set; } = ""; }
    public class ServerStatusResponse { public bool Online { get; set; } public int Players { get; set; } public int MaxPlayers { get; set; } public string Version { get; set; } = ""; }

    public static class DiscordAuth
    {
        public const string CLIENT_ID     = "1507741831658475660";
        public const string CLIENT_SECRET = "j2a1tBBCtM1BXhGinO9boiUY5mOdHOOp";
        private const string REDIRECT_URI = "http://localhost:7777/callback";

        public static string GetAuthUrl() =>
            $"https://discord.com/api/oauth2/authorize?client_id={CLIENT_ID}" +
            $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}&response_type=code&scope=identify";

        public static async Task<DiscordUser?> AuthenticateAsync(Action<string> onStatus)
        {
            try
            {
                onStatus("Opening Discord...");
                Process.Start(new ProcessStartInfo { FileName = GetAuthUrl(), UseShellExecute = true });
                onStatus("Waiting for Discord login...");
                var code = await WaitForCallback();
                if (string.IsNullOrEmpty(code)) { onStatus("Cancelled."); return null; }
                onStatus("Authenticating...");
                return await ExchangeCodeForUser(code);
            }
            catch (Exception ex) { onStatus("Error: " + ex.Message); return null; }
        }

        private static async Task<string?> WaitForCallback()
        {
            using var listener = new System.Net.HttpListener();
            listener.Prefixes.Add("http://localhost:7777/");
            listener.Start();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(2));
            var ctx  = await listener.GetContextAsync().WaitAsync(cts.Token);
            var code = ctx.Request.QueryString["code"];

            string html = "<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'/><title>Stark FN</title><style>html,body{margin:0;padding:0;width:100%;height:100%;background:#0D0D14}body{color:#fff;font-family:'Segoe UI',sans-serif;min-height:100vh;width:100vw;display:flex;align-items:center;justify-content:center;overflow:hidden;position:fixed;inset:0}.glow{position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);width:600px;height:600px;border-radius:50%;background:radial-gradient(circle,rgba(99,102,241,0.12) 0%,transparent 70%);pointer-events:none;z-index:0}.particles{position:fixed;inset:0;pointer-events:none;z-index:0}.p{position:absolute;border-radius:50%;background:rgba(255,255,255,0.15);animation:drift linear infinite}@keyframes drift{0%{transform:translateY(100vh) scale(0)}100%{transform:translateY(-10vh) scale(1)}}.card{position:relative;z-index:1;background:rgba(255,255,255,0.04);border:1px solid rgba(255,255,255,0.1);border-radius:20px;padding:52px 56px;text-align:center;max-width:420px;width:90%;backdrop-filter:blur(20px);box-shadow:0 32px 80px rgba(0,0,0,0.8);animation:slideup 0.5s cubic-bezier(.16,1,.3,1) both}@keyframes slideup{from{opacity:0;transform:translateY(24px)}to{opacity:1;transform:translateY(0)}}.logo{font-size:20px;letter-spacing:0.15em;color:rgba(255,255,255,0.2);margin-bottom:16px}.icon-wrap{width:72px;height:72px;border-radius:50%;background:rgba(99,102,241,0.15);border:1px solid rgba(99,102,241,0.3);display:flex;align-items:center;justify-content:center;margin:0 auto 24px}.icon-wrap svg{width:36px;height:36px}h1{font-size:22px;font-weight:700;margin-bottom:8px}.sub{font-size:14px;color:rgba(255,255,255,0.4);margin-bottom:28px;line-height:1.5}.discord-badge{display:inline-flex;align-items:center;gap:8px;background:rgba(88,101,242,0.15);border:1px solid rgba(88,101,242,0.3);border-radius:8px;padding:8px 16px;font-size:13px;font-weight:600;color:#7289DA;margin-bottom:28px}.bar-wrap{background:rgba(255,255,255,0.06);border-radius:99px;height:3px;overflow:hidden;margin-bottom:16px}.bar{height:100%;border-radius:99px;background:linear-gradient(90deg,#6366F1,#8B5CF6);animation:fill 2s ease-out forwards;box-shadow:0 0 12px rgba(99,102,241,0.6)}@keyframes fill{from{width:0}to{width:100%}}.closing{font-size:12px;color:rgba(255,255,255,0.2)}.season{margin-top:20px;font-size:11px;color:rgba(255,255,255,0.1);letter-spacing:0.08em}</style></head><body><div class='glow'></div><div class='particles' id='pts'></div><div class='card'><div class='logo'>✦ STARK FN</div><div class='icon-wrap'><svg viewBox='0 0 24 24' fill='none' stroke='#6366F1' stroke-width='2.5' stroke-linecap='round' stroke-linejoin='round'><polyline points='20 6 9 17 4 12'/></svg></div><h1>Successfully Authenticated</h1><p class='sub'>Your Discord account has been linked.<br/>You can now return to the launcher.</p><div class='discord-badge'><svg viewBox='0 0 24 24' fill='#7289DA'><path d='M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057c.002.022.015.043.032.054a19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03z'/></svg>Discord Connected</div><div class='bar-wrap'><div class='bar'></div></div><p class='closing'>This window will close automatically...</p><div class='season'>Chapter 2 · Season 4 · 14.60</div></div><script>const pts=document.getElementById('pts');for(let i=0;i<18;i++){const d=document.createElement('div');d.className='p';const s=Math.random()*4+1;d.style.cssText='width:'+s+'px;height:'+s+'px;left:'+(Math.random()*100)+'%;bottom:-10px;opacity:'+(Math.random()*0.3+0.05)+';animation-duration:'+(Math.random()*12+8)+'s;animation-delay:'+(Math.random()*6)+'s';pts.appendChild(d)}setTimeout(()=>window.close(),2200);</script></body></html>";

            byte[] bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.StatusCode = 200;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Flush();
            ctx.Response.OutputStream.Close();
            listener.Stop();
            return code;
        }

        private static async Task<DiscordUser?> ExchangeCodeForUser(string code)
        {
            using var http = new HttpClient();
            var tokenRes = await http.PostAsync("https://discord.com/api/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"]     = CLIENT_ID,
                    ["client_secret"] = CLIENT_SECRET,
                    ["grant_type"]    = "authorization_code",
                    ["code"]          = code,
                    ["redirect_uri"]  = REDIRECT_URI,
                }));
            if (!tokenRes.IsSuccessStatusCode) return null;
            var tokenEl = JsonSerializer.Deserialize<JsonElement>(await tokenRes.Content.ReadAsStringAsync());
            if (!tokenEl.TryGetProperty("access_token", out var atEl)) return null;
            var at = atEl.GetString()!;
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", at);
            var userRes = await http.GetAsync("https://discord.com/api/users/@me");
            if (!userRes.IsSuccessStatusCode) return null;
            var u = JsonSerializer.Deserialize<JsonElement>(await userRes.Content.ReadAsStringAsync());
            return new DiscordUser
            {
                Id            = u.GetProperty("id").GetString()       ?? "",
                Username      = u.GetProperty("username").GetString()  ?? "",
                Discriminator = u.TryGetProperty("discriminator", out var disc) ? disc.GetString() ?? "0" : "0",
                Avatar        = u.TryGetProperty("avatar",        out var av)   ? av.GetString()   ?? ""  : "",
                AccessToken   = at,
            };
        }
    }

    public class DiscordUser
    {
        public string Id            { get; set; } = "";
        public string Username      { get; set; } = "";
        public string Discriminator { get; set; } = "";
        public string Avatar        { get; set; } = "";
        public string AccessToken   { get; set; } = "";
        public string Tag => Discriminator == "0" || string.IsNullOrEmpty(Discriminator)
            ? Username : $"{Username}#{Discriminator}";
        public string AvatarUrl => string.IsNullOrEmpty(Avatar)
            ? "https://cdn.discordapp.com/embed/avatars/0.png"
            : $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.png";
    }

    public static class Session
    {
        static readonly string _dir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StarkFNLauncher");
        static readonly string _file = Path.Combine(_dir, "session.dat");

        public static void Save(string token, string username)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_file, $"{token}\n{username}", Encoding.UTF8);
        }

        public static (string token, string username) Load()
        {
            try
            {
                if (!File.Exists(_file)) return ("", "");
                var lines = File.ReadAllLines(_file, Encoding.UTF8);
                return lines.Length >= 2 ? (lines[0], lines[1]) : ("", "");
            }
            catch { return ("", ""); }
        }

        public static void Clear()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
        }

        public static string GamePath
        {
            get
            {
                try
                {
                    var f = Path.Combine(_dir, "gp.dat");
                    return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
                }
                catch { return ""; }
            }
            set
            {
                Directory.CreateDirectory(_dir);
                File.WriteAllText(Path.Combine(_dir, "gp.dat"), value);
            }
        }
    }
}

// ── FRIENDS ──────────────────────────────────────────────────────
    public class FriendData
    {
        public string Username    { get; set; } = "";
        public string Status      { get; set; } = "offline"; // online / ingame / offline
        public string AvatarUrl   { get; set; } = "";
        public int    Wins        { get; set; }
        public int    Kills       { get; set; }
    }

    public class UpdateData
    {
        public string Version     { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Notes       { get; set; } = "";
        public bool   Required    { get; set; }
    }

    public class IntegrityResult
    {
        public bool   Ok           { get; set; }
        public int    Checked      { get; set; }
        public int    Missing      { get; set; }
        public int    Corrupted    { get; set; }
        public System.Collections.Generic.List<string> Issues { get; set; } = new();
    }
