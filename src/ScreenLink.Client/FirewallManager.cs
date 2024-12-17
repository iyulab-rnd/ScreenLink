using System.Diagnostics;
using System.Security.Principal;

namespace ScreenLink.Client;

public class FirewallManager
{
    private readonly string _appPath;
    private readonly string _appName;

    public FirewallManager(string appName = "ScreenLink")
    {
        _appPath = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot get current process path");
        _appName = appName;
    }

    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void AddFirewallRules()
    {
        if (!IsAdministrator())
        {
            RestartWithAdmin();
            return;
        }

        try
        {
            // Inbound 규칙 추가
            AddFirewallRule($"{_appName}-Inbound", "in");

            // Outbound 규칙 추가
            AddFirewallRule($"{_appName}-Outbound", "out");

            Console.WriteLine("Successfully added firewall rules.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add firewall rules: {ex.Message}");
            throw;
        }
    }

    private void AddFirewallRule(string ruleName, string direction)
    {
        // 기존 규칙 삭제
        ExecuteNetshCommand($"advfirewall firewall delete rule name=\"{ruleName}\"");

        // 새 규칙 추가
        var command = $"advfirewall firewall add rule" +
            $" name=\"{ruleName}\"" +
            $" dir={direction}" +
            $" action=allow" +
            $" program=\"{_appPath}\"" +
            $" enable=yes" +
            $" profile=any" +
            $" localport=any" +
            $" protocol=any" +
            $" edge=yes" +
            $" security=notrequired";

        ExecuteNetshCommand(command);
    }

    private void ExecuteNetshCommand(string command)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null) throw new InvalidOperationException("Failed to start netsh process");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new Exception($"Netsh command failed: {error}");
        }
    }

    private void RestartWithAdmin()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _appPath,
                UseShellExecute = true,
                Verb = "runas" // 관리자 권한으로 실행
            };

            Process.Start(processInfo);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restart with admin privileges: {ex.Message}");
            throw;
        }
    }
}