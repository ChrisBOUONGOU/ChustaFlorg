using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChustaFlorg.Models;
using Microsoft.Maui.Storage;

namespace ChustaFlorg.Services;

public sealed class WorshipSyncService
{
    private const string UrlKey="worship_sync_url",UserKey="worship_sync_user",PasswordKey="worship_sync_password";
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
    public async Task ConfigureAsync(string url,string username,string password)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps)throw new ArgumentException("L'adresse doit être une URL HTTPS valide.");
        await SecureStorage.Default.SetAsync(UrlKey,url.Trim());await SecureStorage.Default.SetAsync(UserKey,username);await SecureStorage.Default.SetAsync(PasswordKey,password);
    }
    public async Task<bool> IsConfiguredAsync()=>!string.IsNullOrWhiteSpace(await SecureStorage.Default.GetAsync(UrlKey));
    public async Task<List<WorshipSong>> SyncAsync(IReadOnlyCollection<WorshipSong> local,CancellationToken token=default)
    {
        var url=await SecureStorage.Default.GetAsync(UrlKey);var user=await SecureStorage.Default.GetAsync(UserKey)??string.Empty;var pass=await SecureStorage.Default.GetAsync(PasswordKey)??string.Empty;
        if(string.IsNullOrWhiteSpace(url))throw new InvalidOperationException("Configurez d'abord une URL WebDAV HTTPS.");
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(25)};
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ChustaFlorg/1.0");
        using var response=await client.GetAsync(url,token);List<WorshipSong> remote;
        if(response.StatusCode==HttpStatusCode.NotFound)remote=[];
        else{response.EnsureSuccessStatusCode();await using var stream=await response.Content.ReadAsStreamAsync(token);remote=await JsonSerializer.DeserializeAsync<List<WorshipSong>>(stream,Options,token)??[];}
        var merged=local.Concat(remote).GroupBy(x=>x.Id).Select(g=>g.OrderByDescending(x=>x.ModifiedAtUtc).First()).OrderBy(x=>x.Title,StringComparer.OrdinalIgnoreCase).ToList();
        using var body=new StringContent(JsonSerializer.Serialize(merged,Options),Encoding.UTF8,"application/json");
        using var upload=await client.PutAsync(url,body,token);upload.EnsureSuccessStatusCode();return merged;
    }
    public void Disconnect(){SecureStorage.Default.Remove(UrlKey);SecureStorage.Default.Remove(UserKey);SecureStorage.Default.Remove(PasswordKey);}
}
