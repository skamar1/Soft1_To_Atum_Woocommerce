using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Soft1_To_Atum.ApiService.Services;

public class WindowsServiceController
{
    private readonly ILogger<WindowsServiceController> _logger;
    private const string ServiceName = "Soft1ToAtumSyncService";

    public WindowsServiceController(ILogger<WindowsServiceController> logger)
    {
        _logger = logger;
    }

    public async Task<ServiceStatus> GetServiceStatusAsync()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ServiceStatus
                {
                    IsRunning = false,
                    Status = "NotAvailable",
                    Message = "Windows Service control is only available on Windows OS"
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {ServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ServiceStatus { IsRunning = false, Status = "Error", Message = "Failed to query service" };
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (output.Contains("RUNNING"))
            {
                return new ServiceStatus { IsRunning = true, Status = "Running", Message = "Service is running" };
            }
            else if (output.Contains("STOPPED"))
            {
                return new ServiceStatus { IsRunning = false, Status = "Stopped", Message = "Service is stopped" };
            }
            else if (output.Contains("does not exist"))
            {
                return new ServiceStatus { IsRunning = false, Status = "NotInstalled", Message = "Service is not installed" };
            }
            else
            {
                return new ServiceStatus { IsRunning = false, Status = "Unknown", Message = output };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service status");
            return new ServiceStatus { IsRunning = false, Status = "Error", Message = ex.Message };
        }
    }

    public async Task<ServiceOperationResult> StartServiceAsync()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ServiceOperationResult
                {
                    Success = false,
                    Message = "Windows Service control is only available on Windows OS"
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"start {ServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ServiceOperationResult { Success = false, Message = "Failed to start service" };
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 || output.Contains("START_PENDING") || output.Contains("RUNNING"))
            {
                _logger.LogInformation("Service started successfully");
                return new ServiceOperationResult { Success = true, Message = "Service started successfully" };
            }
            else
            {
                _logger.LogError("Failed to start service: {Output} {Error}", output, error);
                return new ServiceOperationResult { Success = false, Message = $"Failed to start service: {output} {error}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting service");
            return new ServiceOperationResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<ServiceOperationResult> StopServiceAsync()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ServiceOperationResult
                {
                    Success = false,
                    Message = "Windows Service control is only available on Windows OS"
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"stop {ServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ServiceOperationResult { Success = false, Message = "Failed to stop service" };
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 || output.Contains("STOP_PENDING") || output.Contains("STOPPED"))
            {
                _logger.LogInformation("Service stopped successfully");
                return new ServiceOperationResult { Success = true, Message = "Service stopped successfully" };
            }
            else
            {
                _logger.LogError("Failed to stop service: {Output} {Error}", output, error);
                return new ServiceOperationResult { Success = false, Message = $"Failed to stop service: {output} {error}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping service");
            return new ServiceOperationResult { Success = false, Message = ex.Message };
        }
    }
}

public class ServiceStatus
{
    public bool IsRunning { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ServiceOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
