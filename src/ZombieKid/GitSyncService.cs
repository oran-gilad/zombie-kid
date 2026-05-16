using System.Diagnostics;

namespace ZombieKid;

public sealed class GitSyncService
{
    private readonly GitSyncSettings _settings;
    private DateTime _lastSync = DateTime.MinValue;

    public GitSyncService(GitSyncSettings settings)
    {
        _settings = settings;
    }

    public async Task TrySyncAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.RepositoryDirectory) || !Directory.Exists(_settings.RepositoryDirectory))
        {
            return;
        }

        if (DateTime.Now - _lastSync < TimeSpan.FromMinutes(Math.Max(1, _settings.SyncIntervalMinutes)))
        {
            return;
        }

        _lastSync = DateTime.Now;

        MirrorDataForGitHubPages();
        await RunGitAsync("add data docs", cancellationToken);
        var status = await RunGitAsync("status --porcelain data docs", cancellationToken);
        if (string.IsNullOrWhiteSpace(status.Output))
        {
            return;
        }

        var commit = await RunGitAsync("commit -m \"Update activity data\"", cancellationToken);
        if (commit.ExitCode != 0 && !commit.Output.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await RunGitAsync("push", cancellationToken);
    }

    private async Task<CommandResult> RunGitAsync(string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = _settings.RepositoryDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new CommandResult(1, "Could not start git.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await outputTask) + (await errorTask);
        return new CommandResult(process.ExitCode, output);
    }

    private void MirrorDataForGitHubPages()
    {
        var dataDirectory = Path.Combine(_settings.RepositoryDirectory, "data");
        var docsDataDirectory = Path.Combine(_settings.RepositoryDirectory, "docs", "data");
        if (!Directory.Exists(dataDirectory) || !Directory.Exists(Path.Combine(_settings.RepositoryDirectory, "docs")))
        {
            return;
        }

        Directory.CreateDirectory(docsDataDirectory);
        foreach (var file in Directory.EnumerateFiles(dataDirectory, "*.json"))
        {
            File.Copy(file, Path.Combine(docsDataDirectory, Path.GetFileName(file)), overwrite: true);
        }
    }

    private sealed record CommandResult(int ExitCode, string Output);
}
