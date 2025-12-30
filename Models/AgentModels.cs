namespace WinDbgAssist.Models;

public class AgentDecision
{
    public DecisionType Type { get; set; }
    public string? Command { get; set; }
    public string? Conclusion { get; set; }
    public string Thought { get; set; } = "";
}

public enum DecisionType
{
    NextCommand,
    FinalConclusion
}

public class AnalysisSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Question { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public List<(string Command, string Output, string? Thought)> History { get; set; } = new();
    public AgentDecision? FinalDecision { get; set; }
}
