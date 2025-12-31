# WinDbg Assist

> AI 輔助 WinDbg / Dump 分析工具 - 讓 Production 除錯變簡單

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)
![Azure OpenAI](https://img.shields.io/badge/Azure-OpenAI-0078D4?logo=microsoft-azure)

WinDbg Assist 是一款結合 Azure OpenAI 與 WinDbg (`cdb.exe`) 的自動化診斷工具。它能自主執行連鎖指令，解讀複雜的記憶體 Dump，並產出易於理解的技術報告。

![AI Autonomous Diagnosis Center Demo](images/demo_success.png)

## Features

- **AI 自主診斷** — 輸入問題（如「為什麼發生 Access Violation?」），AI 會自動執行 `!analyze -v`, `!pe`, `!clrstack` 等指令進行連鎖推理
- **診斷歷程追蹤** — 完整記錄 AI 的思考過程與每一步驟的輸出，方便回溯驗證
- **傳統工具集** — 提供指令助手、輸出解讀與 Stack Trace 分析等輔助功能
- **.NET 專精** — 優化的 SOS 載入邏輯，完美支援 .NET 10 的託管例外診斷

## Prerequisites

- **Windows SDK (WinDbg)** — 需要 `cdb.exe`，預設路徑：`C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe`
- **Sysinternals ProcDump** — [官方下載](https://learn.microsoft.com/en-us/sysinternals/downloads/procdump)
- **dotnet-sos** — 執行 `dotnet tool install --global dotnet-sos && dotnet-sos install`
- **.NET 10 SDK**

## Quick Start

### 1. Configuration

Edit `appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4o"
  },
  "Debugger": {
    "CdbPath": "C:\\Program Files (x86)\\Windows Kits\\10\\Debuggers\\x64\\cdb.exe",
    "ProcdumpPath": "C:\\Tools\\procdump64.exe"
  }
}
```

### 2. Run

```bash
dotnet run
```

### 3. Usage

1. Open http://localhost:5187 in your browser
2. Enter the AI Diagnosis Center
3. Click ⚙️ to set your Dump file path
4. Describe your issue and start analysis

## License

MIT License
