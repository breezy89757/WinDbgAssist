using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using WinDbgAssist.Models;

namespace WinDbgAssist.Services;

public class WinDbgAgentService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<WinDbgAgentService> _logger;

    public WinDbgAgentService(IConfiguration config, ILogger<WinDbgAgentService> logger)
    {
        _logger = logger;
        
        var endpoint = config["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("Missing AzureOpenAI:Endpoint");
        var apiKey = config["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("Missing AzureOpenAI:ApiKey");
        var deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
        
        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = client.GetChatClient(deploymentName);
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request)
    {
        var systemPrompt = GetSystemPrompt(request.Type);
        
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(request.Input)
            };
            
            var response = await _chatClient.CompleteChatAsync(messages);
            var content = response.Value.Content[0].Text;
            
            return new AnalysisResult
            {
                Success = true,
                Response = content,
                Explanation = content
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed");
            return new AnalysisResult
            {
                Success = false,
                Response = $"分析失敗: {ex.Message}"
            };
        }
    }

    public async Task<AgentDecision> DetermineNextActionAsync(string description, List<(string Command, string Output)> history)
    {
        var systemPrompt = """
            你是 WinDbg 自動化診斷專家。
            你的目標是根據使用者的問題和目前的執行歷史，找出 Crash 的根本原因。
            
            你可以選擇：
            1. 執行下一個指令：如果你需要更多資訊。
            2. 產出最終結論：如果你已經有足夠的證據解釋當機原因。
            
            輸出格式請嚴格遵守 JSON：
            {
              "Thought": "你的思考過程，說明為什麼你要下這個指令或是得出這個結論",
              "Type": "NextCommand" 或 "FinalConclusion",
              "Command": "如果是 NextCommand，填寫 WinDbg 指令",
              "Conclusion": "如果是 FinalConclusion，填寫診斷總結（包含原因、關鍵點、建議）"
            }
            
            常用指令建議：
            - !analyze -v (全自動分析)
            - ~*k (所有執行緒 call stack)
            - !peb (環境變數與載入路徑)
            - !heap -s (堆積摘要)
            
            針對 .NET/CLR 程式：
            - .load %USERPROFILE%\.dotnet\sos\sos.dll (現代 .NET 10+ 載入 SOS 的方式)
            - !threads (列出所有託管執行緒)
            - !dumpallExceptions (查看所有例外)
            - !clrstack (託管堆疊)
            - !pe (查看當前例外物件)

            ⚠️ 重要規則：
            1. 禁止產生包含角括號 `<...>` 的預留位元指令。如果你需要位址，請先觀察輸出並在下一個步驟使用真實位址，或嘗試自動化指令。
            2. 例外代碼 `04242420` 是 `CLRDBG_NOTIFICATION_EXCEPTION_CODE` (通知性例外)：
               - 這是 .NET 偵錯器的內部通訊，**不是真正的當機**。
               - 如果發現此代碼且 `!pe` 或 `!clrstack` 無內容，請在 `Thought` 中明確指出這是「偵錯器通知」，並直接產出 `FinalConclusion` 告知使用者這不是真正的錯誤。不要跑滿 10 個步驟。
            3. 如果發現 SOS 指令 (!pe, !clrstack) 沒有輸出，可能是符號未正確載入或 SOS 路徑不對，請嘗試 `.load` 絕對路徑或檢查 `.chain`。
            """;

        var userContext = $"[使用者問題]\n{description}\n\n[執行紀錄]\n";
        foreach (var item in history)
        {
            var truncatedOutput = item.Output.Length > 2000 ? item.Output.Substring(0, 2000) + "..." : item.Output;
            userContext += $"指令: {item.Command}\n結果: {truncatedOutput}\n---\n";
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userContext)
            };

            var options = new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() };
            var response = await _chatClient.CompleteChatAsync(messages, options);
            var json = response.Value.Content[0].Text;
            
            var serializerOptions = new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            };
            serializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

            var decision = System.Text.Json.JsonSerializer.Deserialize<AgentDecision>(json, serializerOptions);
            return decision ?? new AgentDecision { Type = DecisionType.NextCommand, Command = "!analyze -v", Thought = "解析失敗，執行預設分析" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to determine next action");
            return new AgentDecision { Type = DecisionType.NextCommand, Command = "!analyze -v", Thought = $"發生錯誤: {ex.Message}" };
        }
    }

    private string GetSystemPrompt(AnalysisType type) => type switch
    {
        AnalysisType.CommandHelper => """
            你是 WinDbg 專家。使用者會描述他想做什麼，你要：
            1. 提供對應的 WinDbg 指令
            2. 解釋這個指令的作用
            3. 提供使用範例
            
            格式：
            ## 指令
            ```
            [WinDbg 指令]
            ```
            
            ## 說明
            [解釋]
            
            ## 範例
            [使用情境]
            """,
            
        AnalysisType.OutputAnalyzer => """
            你是 WinDbg 輸出分析專家。使用者會貼上 WinDbg 的輸出，你要：
            1. 解讀這個輸出的意義
            2. 標出重要的資訊（例如：錯誤碼、記憶體位址、模組名稱）
            3. 建議下一步的調查方向
            
            使用繁體中文回答，技術名詞保持英文。
            """,
            
        AnalysisType.StackTraceAnalyzer => """
            你是 Crash Dump 分析專家。使用者會貼上 Stack Trace 或錯誤訊息，你要：
            1. 分析可能的根本原因（列出 3 個最可能的原因）
            2. 解釋 Stack Trace 中的關鍵函式
            3. 建議的調查步驟
            
            使用繁體中文回答，技術名詞保持英文。保持專業但易懂。
            """,
            
        _ => "你是一個技術助手。"
    };
}
