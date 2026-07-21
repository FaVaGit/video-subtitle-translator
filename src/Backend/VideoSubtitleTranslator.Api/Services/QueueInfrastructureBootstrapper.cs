using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using NATS.Client.Core;
using VideoSubtitleTranslator.Infrastructure.Messaging;

namespace VideoSubtitleTranslator.Api.Services;

public class QueueInfrastructureBootstrapper
{
    private const string DockerContainerName = "vst-nats";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly QueueRuntimeState _queueState;
    private readonly NatsJobPublisher _publisher;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<QueueInfrastructureBootstrapper> _logger;
    private Process? _workerProcess;

    public QueueInfrastructureBootstrapper(
        QueueRuntimeState queueState,
        NatsJobPublisher publisher,
        IHostEnvironment environment,
        ILogger<QueueInfrastructureBootstrapper> logger)
    {
        _queueState = queueState;
        _publisher = publisher;
        _environment = environment;
        _logger = logger;
    }

    public async Task<bool> EnsureQueueInfrastructureAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (await TryEnsureQueueReadyAsync(ct))
            {
                return true;
            }

            _queueState.Status = "bootstrapping";

            if (!await TryStartNatsAsync(ct))
            {
                _queueState.QueueAvailable = false;
                return false;
            }

            if (!await TryEnsureQueueReadyAsync(ct))
            {
                _queueState.QueueAvailable = false;
                return false;
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> TryEnsureQueueReadyAsync(CancellationToken ct)
    {
        try
        {
            await _publisher.EnsureStreamAsync(ct);
        }
        catch (Exception ex) when (ex is NatsException or SocketException or TimeoutException)
        {
            _logger.LogInformation(ex, "Queue broker is not ready yet.");
            return false;
        }

        if (!await EnsureWorkerRunningAsync(ct))
        {
            _logger.LogWarning("Queue broker is reachable but the worker could not be started.");
            return false;
        }

        _queueState.QueueAvailable = true;
        return true;
    }

    private async Task<bool> TryStartNatsAsync(CancellationToken ct)
    {
        if (await IsPortOpenAsync(4222, ct))
        {
            return true;
        }

        var natsBinary = TryResolveNatsBinary();
        if (!string.IsNullOrWhiteSpace(natsBinary))
        {
            var repoRoot = ResolveRepoRoot();
            Directory.CreateDirectory(Path.Combine(repoRoot, "data", "nats"));
            if (TryStartBackgroundProcess(
                natsBinary,
                $"--jetstream --store_dir \"{Path.Combine(repoRoot, "data", "nats")}\"",
                repoRoot,
                out _))
            {
                return await WaitForPortAsync(4222, TimeSpan.FromSeconds(20), ct);
            }
        }

        if (!await CanUseDockerAsync(ct))
        {
            return false;
        }

        await RunProcessAsync("docker", $"start {DockerContainerName}", ResolveRepoRoot(), ct, throwOnError: false);
        var runExitCode = await RunProcessAsync(
            "docker",
            $"run -d --name {DockerContainerName} -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream",
            ResolveRepoRoot(),
            ct,
            throwOnError: false);

        if (runExitCode != 0)
        {
            var startExitCode = await RunProcessAsync("docker", $"start {DockerContainerName}", ResolveRepoRoot(), ct, throwOnError: false);
            if (startExitCode != 0)
            {
                return false;
            }
        }

        return await WaitForPortAsync(4222, TimeSpan.FromSeconds(20), ct);
    }

    private async Task<bool> EnsureWorkerRunningAsync(CancellationToken ct)
    {
        if (_workerProcess is { HasExited: false })
        {
            return true;
        }

        var backendRoot = ResolveBackendRoot();
        var workerProject = Path.Combine(backendRoot, "VideoSubtitleTranslator.Worker", "VideoSubtitleTranslator.Worker.csproj");
        if (!File.Exists(workerProject))
        {
            _logger.LogWarning("Worker project not found at {WorkerProject}.", workerProject);
            return false;
        }

        if (!TryStartBackgroundProcess(
            "dotnet",
            "run --project VideoSubtitleTranslator.Worker --no-build",
            backendRoot,
            out var workerProcess))
        {
            return false;
        }

        if (workerProcess is null)
        {
            return false;
        }

        _workerProcess = workerProcess;
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        return !_workerProcess.HasExited;
    }

    private string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(_environment.ContentRootPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "docker", "docker-compose.yml")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", ".."));
    }

    private string ResolveBackendRoot()
    {
        var contentRoot = new DirectoryInfo(_environment.ContentRootPath);
        return contentRoot.Parent?.FullName ?? _environment.ContentRootPath;
    }

    private static string? TryResolveNatsBinary()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var candidates = pathValue
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path.Trim(), OperatingSystem.IsWindows() ? "nats-server.exe" : "nats-server"))
            .ToList();

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            candidates.Add(Path.Combine(programFiles, "NATS", "nats-server", "nats-server.exe"));
            candidates.Add(Path.Combine(programFilesX86, "NATS", "nats-server", "nats-server.exe"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<bool> CanUseDockerAsync(CancellationToken ct)
    {
        if (await RunProcessAsync("docker", "info", ResolveRepoRoot(), ct, throwOnError: false) != 0)
        {
            return false;
        }

        return true;
    }

    private static bool TryStartBackgroundProcess(string fileName, string arguments, string workingDirectory, out Process? process)
    {
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            return process is not null;
        }
        catch
        {
            process = null;
            return false;
        }
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken ct, bool throwOnError)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            if (throwOnError)
            {
                throw;
            }

            return -1;
        }

        await process.WaitForExitAsync(ct);

        if (throwOnError && process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"Process '{fileName} {arguments}' failed with exit code {process.ExitCode}: {stderr}");
        }

        return process.ExitCode;
    }

    private static async Task<bool> WaitForPortAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await IsPortOpenAsync(port, ct))
            {
                return true;
            }

            await Task.Delay(500, ct);
        }

        return false;
    }

    private static async Task<bool> IsPortOpenAsync(int port, CancellationToken ct)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", port, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}