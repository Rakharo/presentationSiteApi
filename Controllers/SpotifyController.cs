using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using presentationSiteApi.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("[controller]")]
public class SpotifyController : ControllerBase
{
    private readonly AppDbContext _db;

    public SpotifyController(AppDbContext db)
    {
        _db = db;
    }


    [HttpPost("exchange-token")]
    public async Task<IActionResult> ExchangeToken([FromBody] string code)
    {
        if (string.IsNullOrEmpty(code)) return BadRequest("Code is required");

        var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");
        var redirectUri = Environment.GetEnvironmentVariable("SPOTIFY_REDIRECT_URI");

        using var httpClient = new HttpClient();
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var postData = new List<KeyValuePair<string, string>>
    {
        new("grant_type", "authorization_code"),
        new("code", code),
        new("redirect_uri", redirectUri)
    };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(postData)
        };

        var response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return BadRequest(responseContent);

        // Desserialize o JSON
        var json = JsonDocument.Parse(responseContent).RootElement;
        var accessToken = json.GetProperty("access_token").GetString();
        var refreshToken = json.GetProperty("refresh_token").GetString();
        var expiresIn = json.GetProperty("expires_in").GetInt32(); // segundos

        // Salve no banco
        var tokenEntity = new SpotifyToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
        };

        // Se só for um usuário, pode limpar antes:
        var existing = await _db.SpotifyTokens.FirstOrDefaultAsync();
        if (existing != null)
        {
            _db.SpotifyTokens.Remove(existing);
        }
        await _db.SpotifyTokens.AddAsync(tokenEntity);
        await _db.SaveChangesAsync();

        return Ok(new { accessToken, refreshToken, expiresAt = tokenEntity.ExpiresAt });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        var tokenEntity = await _db.SpotifyTokens.FirstOrDefaultAsync();
        if (tokenEntity == null) return BadRequest("Token não encontrado.");

        var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");

        using var httpClient = new HttpClient();
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var postData = new List<KeyValuePair<string, string>>
    {
        new("grant_type", "refresh_token"),
        new("refresh_token", tokenEntity.RefreshToken)
    };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(postData)
        };

        var response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return BadRequest(responseContent);

        var json = JsonDocument.Parse(responseContent).RootElement;
        var accessToken = json.GetProperty("access_token").GetString();
        var expiresIn = json.GetProperty("expires_in").GetInt32();

        // Atualize o token no banco
        tokenEntity.AccessToken = accessToken;
        tokenEntity.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
        await _db.SaveChangesAsync();

        return Ok(new { accessToken, expiresAt = tokenEntity.ExpiresAt });
    }

    [HttpGet("top-artists")]
    public async Task<IActionResult> GetTopArtists()
    {
        string accessToken;
        try
        {
            accessToken = await GetValidAccessToken();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync("https://api.spotify.com/v1/me/top/artists?limit=5");
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return BadRequest(responseContent);

        return Ok(responseContent);
    }

    [HttpGet("top-musics")]
    public async Task<IActionResult> GetTopMusics()
    {
        string accessToken;
        try
        {
            accessToken = await GetValidAccessToken();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync("https://api.spotify.com/v1/me/top/tracks?limit=5");
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return BadRequest(responseContent);

        return Ok(responseContent);
    }


    private async Task<string> GetValidAccessToken()
    {
        var tokenEntity = await _db.SpotifyTokens.FirstOrDefaultAsync();
        if (tokenEntity == null)
            throw new Exception("Token não encontrado. Faça a autenticação primeiro.");

        // Considere um buffer de 1 minuto para evitar race conditions
        if (tokenEntity.ExpiresAt <= DateTime.UtcNow.AddMinutes(1))
        {
            // Atualiza o token
            var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");

            using var httpClient = new HttpClient();
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var postData = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("refresh_token", tokenEntity.RefreshToken)
        };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
            {
                Content = new FormUrlEncodedContent(postData)
            };

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception("Erro ao atualizar token: " + responseContent);

            var json = JsonDocument.Parse(responseContent).RootElement;
            var accessToken = json.GetProperty("access_token").GetString();
            var expiresIn = json.GetProperty("expires_in").GetInt32();

            tokenEntity.AccessToken = accessToken;
            tokenEntity.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            await _db.SaveChangesAsync();
        }

        return tokenEntity.AccessToken;
    }
}