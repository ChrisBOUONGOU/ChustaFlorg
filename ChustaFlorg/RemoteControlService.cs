using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChustaFlorg;

public sealed class RemoteControlService : IAsyncDisposable
{
    private readonly LiveProductionSession _session;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptTask;

    public string Pin { get; } = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    public string LocalAddress { get; }
    public string BaseUrl => $"http://{LocalAddress}:8765";

    public RemoteControlService(LiveProductionSession session)
    {
        _session = session;
        LocalAddress = GetLocalAddress();
        _listener = new TcpListener(IPAddress.Any, 8765);
    }

    public Task StartAsync()
    {
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_stop.Token);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = HandleClientAsync(client, token);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { if (!token.IsCancellationRequested) await Task.Delay(250, token); }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;
                await using var stream = client.GetStream();
                var request = new List<byte>();
                var marker = new byte[] { 13, 10, 13, 10 };
                var matched = 0;
                while (request.Count < 8192 && matched < 4)
                {
                    var one = new byte[1];
                    var read = await stream.ReadAsync(one, token);
                    if (read == 0) return;
                    request.Add(one[0]);
                    matched = one[0] == marker[matched] ? matched + 1 : one[0] == 13 ? 1 : 0;
                }

                var header = Encoding.ASCII.GetString(request.ToArray());
                var firstLine = header.Split("\r\n", StringSplitOptions.None)[0];
                var pieces = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length < 2) return;
                var target = new Uri("http://localhost" + pieces[1]);
                var pinValue = target.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Split('=', 2))
                    .Where(parts => parts.Length == 2 && parts[0] == "pin")
                    .Select(parts => Uri.UnescapeDataString(parts[1]))
                    .FirstOrDefault();
                var validPin = pinValue == Pin;
                var path = target.AbsolutePath.Trim('/').ToLowerInvariant();

                object payload;
                if (!validPin)
                    payload = new { ok = false, error = "unauthorized" };
                else
                {
                    if (path == "next") _session.Move(1);
                    else if (path == "previous") _session.Move(-1);
                    else if (path == "status") { }
                    else
                    {
                        payload = new { ok = false, error = "not_found" };
                        var errorBody = JsonSerializer.SerializeToUtf8Bytes(payload);
                        var errorHeader = Encoding.ASCII.GetBytes($"HTTP/1.1 404 Not Found\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {errorBody.Length}\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(errorHeader, token);
                        await stream.WriteAsync(errorBody, token);
                        return;
                    }
                    payload = Snapshot();
                }

                var body = JsonSerializer.SerializeToUtf8Bytes(payload);
                var responseHeader = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(responseHeader, token);
                await stream.WriteAsync(body, token);
            }
            catch { }
        }
    }

    private object Snapshot() => new
    {
        ok = true,
        index = _session.CurrentIndex,
        count = _session.Slides.Count,
        title = _session.Slides.Count > 0 ? _session.Slides[_session.CurrentIndex].Title : string.Empty
    };

    private static string GetLocalAddress()
    {
        foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
            if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                return address.ToString();
        return "127.0.0.1";
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        if (_acceptTask is not null)
        {
            try { await _acceptTask; } catch { }
        }
        _stop.Dispose();
    }
}
