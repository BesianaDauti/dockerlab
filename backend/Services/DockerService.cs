using System.Diagnostics;

namespace backend.Services;

public class DockerService
{
    private readonly Dictionary<string, (string Id, int Port)> _containers = new();
    private int _nextPort = 5100;
    private readonly object _lock = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public async Task<(string Id, int Port)> GetOrCreateContainer(string username)
    {
        lock (_lock)
        {
            if (_containers.TryGetValue(username, out var existing))
            {
                if (IsContainerRunning(existing.Id))
                    return existing;
                _containers.Remove(username);
            }
        }
        return await CreateContainer(username);
    }

    private async Task<(string Id, int Port)> CreateContainer(string username)
    {
        int port;
        lock (_lock) { port = _nextPort++; }

        RunDocker($"rm -f worker_{username}");

        var result = RunDocker(
            $"run -d --name worker_{username} " +
            $"--memory=512m --cpus=0.5 " +
            $"--memory-swap=512m " +     
            $"-p {port}:5000 " +
            $"codelabs-worker"
        );

        var containerId = result.Trim();
        if (string.IsNullOrEmpty(containerId) || containerId.StartsWith("Error"))
            throw new Exception($"S'u krijua kontejneri: {containerId}");

        await WaitForContainer(port);

        lock (_lock) { _containers[username] = (containerId, port); }
        return (containerId, port);
    }

    private async Task WaitForContainer(int port)
    {
        for (int i = 0; i < 15; i++)
        {
            try
            {
                var r = await _http.GetAsync($"http://localhost:{port}/health");
                if (r.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(1000);
        }
        throw new Exception($"Kontejneri në portin {port} nuk u përgjigj pas 15s");
    }

    public void RemoveContainer(string username)
    {
        lock (_lock)
        {
            if (_containers.TryGetValue(username, out var c))
            {
                RunDocker($"rm -f {c.Id}");
                _containers.Remove(username);
            }
        }
    }

    public List<string> GetActiveUsers()
    {
        lock (_lock) { return _containers.Keys.ToList(); }
    }

    private bool IsContainerRunning(string id)
    {
        var r = RunDocker($"inspect -f {{{{.State.Running}}}} {id}");
        return r.Trim() == "true";
    }

    private string RunDocker(string args)
    {
        var psi = new ProcessStartInfo("docker", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        return string.IsNullOrEmpty(stdout) ? stderr : stdout;
    }
}