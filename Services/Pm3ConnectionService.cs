using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Proxmark3GUI.Models;

namespace Proxmark3GUI.Services;

public sealed class Pm3ConnectionService : IDisposable
{
    private static readonly Lazy<Pm3ConnectionService> _instance = new(() => new Pm3ConnectionService());

    public static Pm3ConnectionService Instance => _instance.Value;

    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 115200;
    public string Pm3ExePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "client", "proxmark3.exe");
    public bool IsConnected => _process is { HasExited: false };

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public event EventHandler<bool>? ConnectionChanged;

    private readonly object _sync = new();
    private Process? _process;
    private StringBuilder? _activeCommandOutput;
    private TaskCompletionSource<string>? _activeCommandCompletion;
    private Timer? _commandIdleTimer;
    private bool _disposed;

    private const int CommandIdleMs = 900;
    private const int CommandTimeoutMs = 45_000;

    private Pm3ConnectionService() { }

    public Task ConnectAsync(string? portName = null)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (!string.IsNullOrWhiteSpace(portName))
                PortName = portName;

            if (string.IsNullOrWhiteSpace(PortName))
                throw new ArgumentException("Select an available serial port before connecting.", nameof(portName));

            if (IsConnected)
                return Task.CompletedTask;

            ResolveLaunchTarget(Pm3ExePath, out var clientExe, out var workingDir);
            Pm3ExePath = clientExe;

            var startInfo = new ProcessStartInfo
            {
                FileName = clientExe,
                Arguments = $"-p {QuoteArg(PortName)} -b {BaudRate} -f",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            startInfo.EnvironmentVariables["PATH"] = BuildMsysPath(workingDir);

            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.Exited += OnProcessExited;

            try
            {
                if (!_process.Start())
                    throw new InvalidOperationException("Failed to start proxmark3 client.");

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch
            {
                _process.OutputDataReceived -= OnOutputDataReceived;
                _process.ErrorDataReceived -= OnErrorDataReceived;
                _process.Exited -= OnProcessExited;
                _process.Dispose();
                _process = null;
                throw;
            }
        }

        ConnectionChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        Process? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
            CompleteActiveCommand(error: new IOException("The Proxmark3 client disconnected while a command was running."));
        }

        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.StandardInput.WriteLine("exit");
                    process.StandardInput.Flush();
                }
                catch
                {
                    // Shutdown path; the process may already be closing.
                }

                if (!process.WaitForExit(1500))
                    process.Kill(entireProcessTree: true);
            }
        }
        finally
        {
            process.Dispose();
            ConnectionChanged?.Invoke(this, false);
        }
    }

    public async Task<string> SendCommandAsync(string command)
    {
        return await SendCommandRawAsync(command, CancellationToken.None);
    }

    public async Task<string> SendCommandRawAsync(string command, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ct.ThrowIfCancellationRequested();

        Process process;
        TaskCompletionSource<string> completion;

        lock (_sync)
        {
            ThrowIfDisposed();

            if (_process is null || _process.HasExited)
                throw new InvalidOperationException("Not connected to Proxmark3.");

            if (_activeCommandCompletion is not null)
                throw new InvalidOperationException("A Proxmark3 command is already running.");

            process = _process;
            _activeCommandOutput = new StringBuilder();
            _activeCommandCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = _activeCommandCompletion;

            try
            {
                // A process can exit between the HasExited check and the pipe write.
                // Never leave an active command session behind if stdin is already closed.
                process.StandardInput.WriteLine(command);
                process.StandardInput.Flush();
                ResetCommandIdleTimer();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                CompleteActiveCommand(error: new IOException("Could not send the command because the Proxmark3 client input pipe is unavailable.", ex));
                throw;
            }
        }

        using var timeoutCts = new CancellationTokenSource(CommandTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        using var registration = linkedCts.Token.Register(() =>
        {
            if (ct.IsCancellationRequested)
                CompleteActiveCommand(error: new OperationCanceledException("The Proxmark3 command was cancelled.", ct));
            else
                CompleteActiveCommand(error: new TimeoutException($"The Proxmark3 command exceeded the {CommandTimeoutMs} ms timeout."));
        });

        return await completion.Task;
    }

    public Task<string> SendCustomCommandAsync(string cmd) => SendCommandAsync(cmd);

    public async Task<Pm3DeviceInfo> GetDeviceInfoAsync()
    {
        var raw = string.Join(Environment.NewLine, await SendCommandAsync("hw version"), await SendCommandAsync("hw status"));
        return ParseDeviceInfo(raw);
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var isCurrentProcess = false;
        lock (_sync)
        {
            if (ReferenceEquals(_process, sender))
            {
                _process = null;
                isCurrentProcess = true;
            }
        }

        if (!isCurrentProcess)
            return;

        CompleteActiveCommand(error: new IOException("The Proxmark3 client process exited while a command was running."));
        ConnectionChanged?.Invoke(this, false);
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        HandleClientLine(e.Data, OutputReceived);
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        HandleClientLine(e.Data, ErrorReceived);
    }

    private void HandleClientLine(string? rawLine, EventHandler<string>? eventHandler)
    {
        if (!ClientOutputProtocol.TryNormalizeLine(rawLine, out var line))
            return;

        if (ClientOutputProtocol.IsCompletionMarker(line))
        {
            CompleteActiveCommand();
            return;
        }

        eventHandler?.Invoke(this, line);

        lock (_sync)
        {
            if (_activeCommandOutput is not null)
            {
                _activeCommandOutput.AppendLine(line);
                ResetCommandIdleTimer();

            }
        }
    }

    private void ResetCommandIdleTimer()
    {
        _commandIdleTimer ??= new Timer(_ => CompleteActiveCommand(), null, Timeout.Infinite, Timeout.Infinite);
        _commandIdleTimer.Change(CommandIdleMs, Timeout.Infinite);
    }

    private void CompleteActiveCommand(string? forcedOutput = null, Exception? error = null)
    {
        TaskCompletionSource<string>? completion;
        string output;

        lock (_sync)
        {
            completion = _activeCommandCompletion;
            if (completion is null)
                return;

            output = forcedOutput ?? _activeCommandOutput?.ToString().TrimEnd() ?? string.Empty;
            _activeCommandCompletion = null;
            _activeCommandOutput = null;
            _commandIdleTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        if (error is OperationCanceledException cancellation)
            completion.TrySetCanceled(cancellation.CancellationToken);
        else if (error is not null)
            completion.TrySetException(error);
        else
            completion.TrySetResult(output);
    }

    private static Pm3DeviceInfo ParseDeviceInfo(string raw)
    {
        var info = new Pm3DeviceInfo { IsConnected = true };

        foreach (var rawLine in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().Trim('[', '+', ']', ' ');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.Contains(".ncd", StringComparison.OrdinalIgnoreCase) && info.FpgaVersion.Length == 0)
            {
                info.FpgaVersion = line;
                continue;
            }

            var parts = SplitDeviceInfoKeyValue(line);
            if (parts.Length < 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (key.Contains("firmware", StringComparison.OrdinalIgnoreCase))
                info.FirmwareVersion = value;
            else if (key.Contains("platform", StringComparison.OrdinalIgnoreCase) || key.Contains("hardware", StringComparison.OrdinalIgnoreCase))
                info.HardwareVersion = value;
            else if (key.Contains("unique", StringComparison.OrdinalIgnoreCase) || key.Equals("uid", StringComparison.OrdinalIgnoreCase))
                info.Pm3Uid = value;
            else if (key.Contains("fpga", StringComparison.OrdinalIgnoreCase) && info.FpgaVersion.Length == 0)
                info.FpgaVersion = value;
            else if (key.Contains("flash", StringComparison.OrdinalIgnoreCase))
                info.FlashSize = value;
            else if (key.Contains("serial", StringComparison.OrdinalIgnoreCase))
                info.SerialNumber = value;
            else if (key.Contains("bootloader", StringComparison.OrdinalIgnoreCase))
                info.BootloaderVersion = value;
            else if (key.Contains("battery", StringComparison.OrdinalIgnoreCase))
                info.BatteryVoltage = value;
            else if (key.Contains("usb", StringComparison.OrdinalIgnoreCase))
                info.UsbVoltage = value;
            else if (key.Contains("field", StringComparison.OrdinalIgnoreCase))
                info.FieldVoltage = value;
            else if (key.Contains("hf", StringComparison.OrdinalIgnoreCase) && key.Contains("peak", StringComparison.OrdinalIgnoreCase))
                info.HfPeakV = value;
            else if (key.Contains("hf", StringComparison.OrdinalIgnoreCase) && key.Contains("freq", StringComparison.OrdinalIgnoreCase))
                info.HfFreq = value;
            else if (key.Contains("lf", StringComparison.OrdinalIgnoreCase) && key.Contains("peak", StringComparison.OrdinalIgnoreCase))
                info.LfPeakV = value;
            else if (key.Contains("lf", StringComparison.OrdinalIgnoreCase) && key.Contains("freq", StringComparison.OrdinalIgnoreCase))
                info.LfFreq = value;
            else if (key.Contains("mode", StringComparison.OrdinalIgnoreCase) && value.Contains(".ncd", StringComparison.OrdinalIgnoreCase) && info.FpgaVersion.Length == 0)
                info.FpgaVersion = value;
            else if (key.Contains(".ncd", StringComparison.OrdinalIgnoreCase) && info.FpgaVersion.Length == 0)
                info.FpgaVersion = line;
        }

        return info;
    }

    private static string[] SplitDeviceInfoKeyValue(string line)
    {
        var colonIdx = line.IndexOf(':');
        if (colonIdx >= 0)
            return new[] { line[..colonIdx], line[(colonIdx + 1)..] };

        var equalsIdx = line.IndexOf('=');
        if (equalsIdx >= 0)
            return new[] { line[..equalsIdx], line[(equalsIdx + 1)..] };

        var match = System.Text.RegularExpressions.Regex.Match(line, @"^(.*?)\.{{2,}}\s*(.+)$");
        if (match.Success)
            return new[] { match.Groups[1].Value, match.Groups[2].Value };

        match = System.Text.RegularExpressions.Regex.Match(line, @"^(.+?)\s{2,}(.+)$");
        if (match.Success)
            return new[] { match.Groups[1].Value, match.Groups[2].Value };

        return Array.Empty<string>();
    }

    private static void ResolveLaunchTarget(string? configuredPath, out string clientExe, out string workingDir)
    {
        // The Settings view explicitly asks the operator to select an executable.
        // Honour that path rather than silently launching a different bundled client.
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var selectedPath = Path.GetFullPath(configuredPath.Trim());
            if (!File.Exists(selectedPath))
                throw new FileNotFoundException("The configured Proxmark3 executable does not exist.", selectedPath);

            clientExe = selectedPath;
            workingDir = Path.GetDirectoryName(selectedPath) ?? Environment.CurrentDirectory;
            return;
        }

        var bundledPath = Path.Combine(AppContext.BaseDirectory, "client", "proxmark3.exe");
        if (!File.Exists(bundledPath))
            throw new FileNotFoundException("Could not find the bundled client\\proxmark3.exe.", bundledPath);

        clientExe = bundledPath;
        workingDir = Path.GetDirectoryName(clientExe) ?? Environment.CurrentDirectory;
    }

    private static string BuildMsysPath(string clientDir)
    {
        var dirs = new[]
        {
            clientDir,
            @"C:\ProxSpace\msys2\mingw64\bin",
            @"C:\ProxSpace\msys2\usr\bin",
            @"C:\msys64\mingw64\bin",
            @"C:\msys64\usr\bin",
            @"C:\msys64\ucrt64\bin",
        }.Where(Directory.Exists);

        return string.Join(Path.PathSeparator, dirs.Concat(new[] { Environment.GetEnvironmentVariable("PATH") ?? string.Empty }));
    }

    private static string QuoteArg(string arg) => $"\"{arg.Replace("\"", "\\\"")}\"";

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Pm3ConnectionService));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Disconnect();
        _commandIdleTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
