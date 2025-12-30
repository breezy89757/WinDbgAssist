namespace WinDbgAssist.Models;

public class DebuggerSettings
{
    public string CdbPath { get; set; } = @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe";
    public string ProcdumpPath { get; set; } = "";
}

public class DebuggerCommandRequest
{
    public string DumpPath { get; set; } = "";
    public string Command { get; set; } = "";
}

public class DebuggerResponse
{
    public string Output { get; set; } = "";
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string? CapturedDumpPath { get; set; }
}
