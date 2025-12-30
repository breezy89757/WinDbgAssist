namespace WinDbgAssist.Models;

public class AnalysisRequest
{
    public string Input { get; set; } = "";
    public AnalysisType Type { get; set; }
}

public enum AnalysisType
{
    CommandHelper,      // 描述需求 → 產生 WinDbg 指令
    OutputAnalyzer,     // WinDbg 輸出 → 解讀
    StackTraceAnalyzer  // Stack Trace → 根因分析
}

public class AnalysisResult
{
    public bool Success { get; set; }
    public string Response { get; set; } = "";
    public string? Command { get; set; }  // For CommandHelper
    public string? Explanation { get; set; }
    public List<string> PossibleCauses { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
