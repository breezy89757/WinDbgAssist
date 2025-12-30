using System.Diagnostics;
using WinDbgAssist.Models;

namespace WinDbgAssist.Services;

public class DebuggerService
{
    private readonly string _cdbPath;
    private readonly string _procdumpPath;
    private readonly ILogger<DebuggerService> _logger;

    public DebuggerService(IConfiguration config, ILogger<DebuggerService> logger)
    {
        _logger = logger;
        _cdbPath = config["Debugger:CdbPath"] ?? @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe";
        _procdumpPath = config["Debugger:ProcdumpPath"] ?? "";

        // 註冊編碼提供者以支援 Big5 (CP950)
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public async Task<DebuggerResponse> CaptureDumpAsync(string target, string outputFolder)
    {
        if (!File.Exists(_procdumpPath))
        {
            return new DebuggerResponse { Success = false, ErrorMessage = "找不到 procdump.exe" };
        }

        try
        {
            // 如果 target 是檔案名稱而非絕對路徑，我們嘗試在幾個常見地方尋找
            var targetExe = target;
            if (!Path.IsPathRooted(targetExe))
            {
                if (!targetExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    targetExe += ".exe";
                }
                
                // 優先檢查當前目錄，以及使用者指定的 TestTools 目錄（如果有的話）
                var fullPath = Path.GetFullPath(targetExe);
                if (!File.Exists(fullPath))
                {
                    // 這裡可以加入更多搜尋路徑，或者乾脆要求使用者輸入完整路徑
                    _logger.LogWarning("找不到目標程式: {Target}", targetExe);
                }
                else
                {
                    targetExe = fullPath;
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _procdumpPath,
                // 使用正向過濾，只捕捉真正的當機 (ACCESS_VIOLATION 或 CLR 例外)，排除掉偵錯器啟動的雜訊
                Arguments = $"-accepteula -e 1 -f ACCESS_VIOLATION -ma -x \"{outputFolder}\" \"{targetExe}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.GetEncoding(950),
                StandardErrorEncoding = System.Text.Encoding.GetEncoding(950),
                WorkingDirectory = Path.GetDirectoryName(targetExe) ?? AppDomain.CurrentDomain.BaseDirectory
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 從輸出中解析產生的檔案路徑
            string? capturedPath = null;
            if (process.ExitCode == 0)
            {
                var match = System.Text.RegularExpressions.Regex.Match(output, @"Dump written to (.*\.dmp)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    capturedPath = match.Groups[1].Value.Trim();
                    _logger.LogInformation("成功捕捉 Dump 至: {Path}", capturedPath);
                }
            }

            // 只要輸出包含 "Dump written" 或 "Dump 1 complete"，就算成功（即使 ExitCode 可能是 1）
            bool captureSucceeded = output.Contains("Dump written", StringComparison.OrdinalIgnoreCase) || 
                                    output.Contains("Dump 1 complete", StringComparison.OrdinalIgnoreCase);

            return new DebuggerResponse
            {
                Success = captureSucceeded || process.ExitCode == 0,
                Output = output + "\n" + error,
                ErrorMessage = (!captureSucceeded && process.ExitCode != 0) ? $"ProcDump ExitCode: {process.ExitCode}" : ""
            };
        }
        catch (Exception ex)
        {
            return new DebuggerResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<DebuggerResponse> ExecuteCommandAsync(string dumpPath, string command)
    {
        if (!File.Exists(_cdbPath))
        {
            return new DebuggerResponse 
            { 
                Success = false, 
                ErrorMessage = $"找不到 cdb.exe，請確認路徑設定: {_cdbPath}" 
            };
        }

        if (!File.Exists(dumpPath))
        {
            return new DebuggerResponse 
            { 
                Success = false, 
                ErrorMessage = $"找不到 Dump 檔案: {dumpPath}" 
            };
        }

        try
        {
            var big5 = System.Text.Encoding.GetEncoding(950);

            var startInfo = new ProcessStartInfo
            {
                FileName = _cdbPath,
                Arguments = $"-z \"{dumpPath}\" -c \"{command}; q\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = big5,
                StandardErrorEncoding = big5,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new DebuggerResponse
            {
                Success = process.ExitCode == 0,
                Output = output,
                ErrorMessage = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "cdb.exe execution failed");
            return new DebuggerResponse { Success = false, ErrorMessage = ex.Message };
        }
    }
}
