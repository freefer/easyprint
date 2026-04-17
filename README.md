English | [中文](./README.zh.md)

# EasyPrint — Silent Local HTML Label Printing Service

EasyPrint is a **label printing solution** that runs natively on Windows, consisting of two sub-projects:

| Sub-project | Directory | Language | Role |
|-------------|-----------|----------|------|
| **Server** | `src/EasyPrint/` | C# .NET 8 WinForms | WebSocket service + rendering + printing |
| **Client SDK** | `easyprint-js/` | TypeScript | Browser-side WebSocket wrapper |

---

## Table of Contents

- [Architecture](#architecture)
- [Server Internals](#server-internals)
  - [Startup Flow](#startup-flow)
  - [WebSocket Protocol](#websocket-protocol)
  - [Print Pipeline](#print-pipeline)
  - [Printer Info & Queue Management](#printer-info--queue-management)
  - [UI Monitor Panel](#ui-monitor-panel)
  - [Configuration File](#configuration-file)
  - [Dependencies](#dependencies)
- [Client SDK Internals](#client-sdk-internals)
  - [Connection & Reconnect](#connection--reconnect)
  - [Message Queue](#message-queue)
  - [Event System](#event-system)
- [Full Data Flow](#full-data-flow)
- [Quick Start](#quick-start)
  - [Run the Server](#run-the-server)
  - [Use the SDK](#use-the-sdk)
  - [Angular Integration](#angular-integration)
- [Protocol Reference](#protocol-reference)
- [Project Structure](#project-structure)

---

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Browser / Angular App                           │
│                                                  │
│  ┌──────────────────────────────────────────┐   │
│  │  @easyprint/js  (easyprint-js SDK)       │   │
│  │  EasyPrintClient                         │   │
│  │  · WebSocket connection management       │   │
│  │  · Exponential backoff auto-reconnect    │   │
│  │  · Offline message queue                 │   │
│  │  · Event-driven API                      │   │
│  └──────────────┬───────────────────────────┘   │
│                 │ ws://127.0.0.1:8765            │
└─────────────────┼───────────────────────────────┘
                  │ WebSocket (TCP)
┌─────────────────┼───────────────────────────────┐
│  EasyPrint Server (Windows WinForms)             │
│                 │                                │
│  SuperSocket WebSocket Server                    │
│        │                                         │
│  JsonPackageConverter                            │
│  "PRINT {...}" → StringPackageInfo               │
│        │                                         │
│  Command routing (PRINT/LIST/JOBS/CANCEL/...)    │
│        │                                         │
│  ┌─────┴──────────────────────────────┐          │
│  │  PuppeteerSharp (Chromium)         │          │
│  │  HTML → PDF (exact mm dimensions)  │          │
│  └─────┬──────────────────────────────┘          │
│        │                                         │
│  ┌─────┴──────────────────────────────┐          │
│  │  PDFtoImage (SkiaSharp)            │          │
│  │  PDF → Bitmap (native printer DPI) │          │
│  └─────┬──────────────────────────────┘          │
│        │                                         │
│  ┌─────┴──────────────────────────────┐          │
│  │  System.Drawing.Printing           │          │
│  │  PrintDocument → printer driver    │          │
│  └─────┬──────────────────────────────┘          │
│        │                                         │
│  PrinterHelper (WinSpool P/Invoke)               │
│  · Paper size & DPI (<1ms)                       │
│  · Enumerate & control print queue jobs          │
│                                                  │
│  PrintSpoolerWatcher                             │
│  · FindFirstPrinterChangeNotification            │
│  · Event-driven queue sync (zero polling)        │
│                                                  │
│  Form1 (WinForms UI)                             │
│  · Live print queue panel (Spooler sync)         │
│  · Real-time log panel                           │
│  · Service configuration (IP/Port)               │
└──────────────────────────────────────────────────┘
                  │
           Physical printer (USB/Network)
```

---

## Server Internals

### Startup Flow

```
Program.Main()
  │
  ├─ AppDataContext.LoadConfig()         Read cfg.json (IP, Port, AutoStart)
  │
  ├─ WebSocketHostBuilder.Create()       Build SuperSocket WebSocket host
  │    ├─ UseSession<EasyPrintSession>()
  │    ├─ UseHostedService<EasyPrintService>()
  │    └─ UseCommand<StringPackageInfo, JsonPackageConverter>()
  │         ├─ AddCommand<PRINT>()
  │         ├─ AddCommand<LIST>()
  │         ├─ AddCommand<JOBS>()
  │         ├─ AddCommand<CANCEL>()
  │         ├─ AddCommand<RESTART>()
  │         ├─ AddCommand<PAUSE>()
  │         └─ AddCommand<RESUME>()
  │
  ├─ new Form1(cfg, host, loggerProvider)
  │    ├─ SetupDataGrid()                Bind BindingList<PrintQueueJob>
  │    ├─ DownloadBrowserAsync()         Background Chromium download
  │    └─ OnShown
  │         ├─ StartService()            Start WebSocket service
  │         ├─ LaunchAsync()             Start Chromium
  │         ├─ SyncPrintQueue()          Initial full queue load
  │         └─ PrintSpoolerWatcher.Start() Start event-driven queue watch
  │
  └─ Application.Run(form)              Enter WinForms message loop
```

### WebSocket Protocol

The server uses **SuperSocket 2.x**. The core is `JsonPackageConverter`:

```csharp
// JsonPackageConverter.cs
public StringPackageInfo Map(WebSocketPackage package)
{
    var arr = package.Message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    pack.Key        = arr[0];                // "PRINT" / "JOBS" / ...
    pack.Parameters = arr.Skip(1).ToArray(); // ["{...}"] (empty when no JSON body)
}
```

`Split(' ', 2)` splits only at the **first space**, so HTML content with spaces is completely safe.

### Print Pipeline

The `PRINT` command handler (`Command/PRINT.cs`) executes these steps:

```
① Receive PrintJob
   ├─ PrinterName empty → call PrinterHelper.GetDefaultPrinterName()
   └─ WidthMm/HeightMm = 0 → call PrinterHelper.GetDefaultPaperSizeMm()

② HTML → PDF (PuppeteerSharp)
   ├─ Width/Height = exact mm dimensions
   ├─ PrintBackground = true   (preserve background colors for barcodes)
   └─ MarginOptions = 0        (zero margins)

③ PDF → Bitmap (PDFtoImage + SkiaSharp)
   └─ Rasterize at native printer DPI

④ Bitmap → Printer (System.Drawing.Printing)
   └─ Adaptive DPI scaling → centered output → printer driver

⑤ Send response to client
```

### Printer Info & Queue Management

`PrinterHelper` uses **WinSpool Win32 P/Invoke** to read printer configuration and control the print queue directly.

**Paper size & DPI reading (<1ms):**

```
OpenPrinter() → DocumentProperties() → DEVMODE struct
  dmPaperWidth/Length → paper size (0.1mm units)
  dmPrintQuality/dmYResolution → native printer DPI
```

**Print queue operations (EnumJobs / SetJob):**

| Method | Description |
|--------|-------------|
| `GetPrintJobs(printerName?)` | List all jobs in the specified printer queue |
| `CancelPrintJob(printer, jobId)` | Cancel (delete) a specific job |
| `RestartPrintJob(printer, jobId)` | Restart a job from the beginning |
| `PausePrintJob(printer, jobId)` | Pause a specific job |
| `ResumePrintJob(printer, jobId)` | Resume a paused job |

**Event-driven queue monitoring (`PrintSpoolerWatcher`):**

Uses `FindFirstPrinterChangeNotification` + `WaitForMultipleObjects` to register job change events for every installed printer. When a job is added (`ADD_JOB`), updated (`SET_JOB`), or removed (`DELETE_JOB`), the background thread immediately notifies the UI to sync — no polling needed.

### UI Monitor Panel

`Form1` provides the following features:

- **Service config**: Listen IP, port, start/stop service, startup-with-Windows toggle
- **Chromium download**: Background download of ChromeHeadlessShell on first run with status bar animation
- **Live print queue panel**:
  - Data source: `BindingList<PrintQueueJob>`, shows real-time Windows print queue
  - `PrintQueueJob` implements `INotifyPropertyChanged` — row auto-refreshes on status change
  - Columns: Queue ID / Printer / Document / User / Pages / Status
  - Filter: All / Printing / Error
  - Action buttons: ⏸ Pause All / ▶ Resume All / ✕ Cancel All / ⚠ Clear Queue
- **Log panel**: Dark terminal-style `RichTextBox` with color-coded log levels

### Configuration File

Stored at `{exe_dir}/data/cfg.json`, auto-generated on first run:

```json
{
  "Ip":                "Any",
  "Port":              8765,
  "AutoStart":         true,
  "MaxPackageLength":  1022886006,
  "ReceiveBufferSize": 409600
}
```

| Field | Description |
|-------|-------------|
| `Ip` | Listen address; `"Any"` = all network interfaces |
| `Port` | Listen port |
| `AutoStart` | Auto-start WebSocket service when the window appears |
| `MaxPackageLength` | Max message size in bytes (~1 GB), supports large HTML payloads |
| `ReceiveBufferSize` | Receive buffer size |

### Dependencies

| Library | Version | Purpose |
|---------|---------|---------|
| SuperSocket | 2.0.2 | WebSocket server framework |
| SuperSocket.WebSocket.Server | 2.0.2 | WebSocket protocol support |
| PuppeteerSharp | 24.40.0 | Chromium control (HTML → PDF) |
| PDFtoImage | 5.2.0 | PDF → SKBitmap rasterization (PDFium-based) |
| SkiaSharp | — | Image processing (pulled in by PDFtoImage) |
| ReaLTaiizor | 3.8.1.4 | WinForms dark theme UI controls |
| Newtonsoft.Json | 13.0.3 | JSON serialization |

---

## Client SDK Internals

### Connection & Reconnect

`EasyPrintClient` initiates a connection immediately on construction:

```
constructor()
  └─ connect()
       ├─ new WebSocket(url)
       ├─ onopen  → setState('connected')  + flush pendingQueue
       ├─ onerror → setState('error')      (onclose fires after)
       └─ onclose → setState('disconnected')
                      └─ code !== 1000 && autoReconnect
                           └─ scheduleReconnect()
                                └─ setTimeout(connect, currentDelay)
                                     └─ currentDelay = min(delay*2, maxDelay)
```

**Exponential backoff sequence (default config):**

```
1st disconnect → wait 1s  → reconnect
2nd disconnect → wait 2s  → reconnect
3rd disconnect → wait 4s  → reconnect
...
6th+ disconnect → wait 30s → reconnect (cap)
Reconnect OK   → reset to 1s
```

### Message Queue

When any send method is called while disconnected, the message enters `pendingQueue`:

```
send() → [connected]    → ws.send(message)
       → [disconnected] → pendingQueue.push(message)
                            emit('queued', message, queueLength)

onopen → while (pendingQueue.length > 0)
           ws.send(pendingQueue.shift())  ← preserves send order
```

### Event System

Built-in lightweight `TypedEmitter` (no Node.js dependency, works in all browsers):

| Event | Trigger | Parameters |
|-------|---------|------------|
| `connecting` | Connection attempt started | — |
| `connected` | Connected, pending queue flushed | — |
| `disconnected` | Connection closed | `code, reason` |
| `error` | WebSocket error | `Event` |
| `response` | Server response received (all commands) | `PrintResponse` |
| `stateChange` | Connection state changed | `ConnectionState` |
| `queued` | Message queued due to disconnect | `message, queueLength` |
| `reconnecting` | Waiting to reconnect | `delay, attempt` |

---

## Full Data Flow

```
Browser
  │  client.print({ context: '<html>W0100025</html>', widthMm: 76, heightMm: 130 })
  │  WS frame: PRINT {"printerName":"","context":"...","widthMm":76,"heightMm":130}
  ▼
SuperSocket → JsonPackageConverter → PRINT command handler
  ├─ HTML → PDF → Bitmap → printer driver
  └─ Response: {"command":"PRINT","status":200,"data":"A3F1B2C0"}

  │  client.jobs('TSC TE200')
  │  WS frame: JOBS {"printerName":"TSC TE200"}
  ▼
JOBS command handler
  ├─ PrinterHelper.GetPrintJobs('TSC TE200')
  └─ Response: {"command":"JOBS","status":200,"data":[{"jobId":1,...}]}

  │  client.cancel([1,2], 'TSC TE200')
  │  WS frame: CANCEL {"printerName":"TSC TE200","jobIds":[1,2]}
  ▼
CANCEL command handler
  ├─ PrinterHelper.CancelPrintJob('TSC TE200', 1)
  ├─ PrinterHelper.CancelPrintJob('TSC TE200', 2)
  └─ Response: {"command":"CANCEL","status":200,"data":[{"jobId":1,"ok":true},{"jobId":2,"ok":true}]}
```

---

## Quick Start

### Run the Server

**Requirements:** Windows 10/11 · .NET 8 SDK · Windows GDI printing support

Download `win-x64.zip` from the [Releases page](https://github.com/freefer/easyprint/releases), extract and run `EasyPrint.exe`.

On first launch, ChromeHeadlessShell (~50 MB) is downloaded automatically and cached for future use.

Or build from source:

```powershell
cd src/EasyPrint
dotnet run                                                            # development
dotnet publish -c Release -r win-x64 --self-contained true           # standalone exe
```

The configuration file is auto-generated at `{exe_dir}/data/cfg.json` on first run.

---

### Use the SDK

**Install:**

```bash
npm install @easyprint/js
```

**Basic usage:**

```typescript
import { EasyPrintClient, PrintQueueJob, JobControlResult } from '@easyprint/js';

const client = new EasyPrintClient({ host: '127.0.0.1', port: 8765 });

client.on('connected',    () => console.log('Connected'));
client.on('disconnected', (code) => console.log('Disconnected', code));

client.on('response', resp => {
  switch (resp.command) {
    case 'PRINT':
      console.log('Print job submitted, ID:', resp.data);
      break;
    case 'JOBS':
      const jobs = resp.data as PrintQueueJob[];
      console.log('Queue jobs:', jobs);
      break;
    case 'CANCEL':
      const results = resp.data as JobControlResult[];
      results.forEach(r => console.log(`Job ${r.jobId}: ${r.ok ? 'cancelled' : 'failed'}`));
      break;
  }
});

// Print an HTML label
client.print({
  context:     '<html><body style="margin:0">W0100025</body></html>',
  widthMm:     76,
  heightMm:    130,
  printerName: 'TSC TE200',   // omit to use the server's default printer
});

// Query print queue
client.jobs('TSC TE200');

// Batch job control
client.pause([1, 2, 3], 'TSC TE200');
client.resume([1, 2, 3], 'TSC TE200');
client.cancel([4], 'TSC TE200');
```

---

### Angular Integration

**environment.ts:**

```typescript
export const environment = {
  easyPrint: { host: '127.0.0.1', port: 8765 },
};
```

**print.service.ts:**

```typescript
import { Injectable, OnDestroy } from '@angular/core';
import { Observable } from 'rxjs';
import { EasyPrintClient, PrintResponse } from '@easyprint/js';
import { environment } from 'src/environments/environment';

@Injectable({ providedIn: 'root' })
export class PrintService implements OnDestroy {
  private readonly client = new EasyPrintClient(environment.easyPrint);

  readonly response$ = new Observable<PrintResponse>(observer => {
    const handler = (resp: PrintResponse) => observer.next(resp);
    this.client.on('response', handler);
    return () => this.client.off('response', handler);
  });

  printLabel(html: string, widthMm = 76, heightMm = 130): void {
    this.client.print({ context: html, widthMm, heightMm });
  }

  ngOnDestroy(): void { this.client.destroy(); }
}
```

---

## Protocol Reference

All commands follow the format: `COMMAND <JSON body>` or a bare command string (when no body is needed).

### Command Overview

| Command | Send Format | `data` Type | Description |
|---------|-------------|-------------|-------------|
| `PRINT` | `PRINT {PrintJob}` | `string` (job ID) | Print HTML content |
| `LIST` | `LIST` | `PrinterInfo[]` | List installed printers |
| `JOBS` | `JOBS {printerName?}` | `PrintQueueJob[]` | Query print queue |
| `CANCEL` | `CANCEL {printerName?, jobIds[]}` | `JobControlResult[]` | Cancel jobs |
| `RESTART` | `RESTART {printerName?, jobIds[]}` | `JobControlResult[]` | Restart jobs |
| `PAUSE` | `PAUSE {printerName?, jobIds[]}` | `JobControlResult[]` | Pause jobs |
| `RESUME` | `RESUME {printerName?, jobIds[]}` | `JobControlResult[]` | Resume jobs |

### Unified Response Format

```json
{
  "command": "PRINT",
  "status":  200,
  "message": "Print successful",
  "data":    "A3F1B2C0"
}
```

| Field | Description |
|-------|-------------|
| `command` | The command that triggered this response |
| `status` | `200` success / `207` partial success / `400` bad request / `500` server error |
| `message` | Human-readable result description |
| `data` | Response payload; type varies by command |

### PRINT Request Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `printerName` | string | `""` | Printer name; empty = system default |
| `context` | string | **required** | Full HTML string to print |
| `widthMm` | number | `0` | Label width in mm; `0` = auto-read from printer |
| `heightMm` | number | `0` | Label height in mm; `0` = auto-read from printer |
| `paddingMm` | number[] | `[0,0,0,0]` | Page padding in mm: top, right, bottom, left |

### Batch Control Request Fields (CANCEL / RESTART / PAUSE / RESUME)

| Field | Type | Description |
|-------|------|-------------|
| `printerName` | string? | Printer name; empty = default printer |
| `jobIds` | number[] | List of job IDs to operate on (one or more) |

---

## Project Structure

```
EasyPrint/
├── src/
│   └── EasyPrint/                    C# WinForms server
│       ├── Program.cs                Entry point, registers all commands
│       ├── AppSettings.cs            Config model (IP/Port/AutoStart)
│       ├── AppDataContext.cs         Config file read/write
│       ├── EasyPrintService.cs       SuperSocket service
│       ├── EasyPrintSession.cs       WebSocket session (SendMessage)
│       ├── PrintJob.cs               EasyPrint print job model
│       ├── PrintJobStatus.cs         Job status enum
│       ├── PrintQueueJob.cs          Windows queue job model (INotifyPropertyChanged)
│       ├── PrintResponseMessage.cs   Response message model
│       ├── PrinterHelper.cs          WinSpool P/Invoke (paper/DPI/queue R/W)
│       ├── PrintSpoolerWatcher.cs    Event-driven print queue monitor
│       ├── UiLogger.cs               Log routing to UI panel
│       ├── Form1.cs                  Main window business logic
│       ├── Form1.Designer.cs         WinForms dark theme UI layout
│       └── Command/
│           ├── PRINT.cs              PRINT command (HTML→PDF→print)
│           ├── LIST.cs               LIST command (enumerate printers)
│           ├── JOBS.cs               JOBS command (query print queue)
│           ├── CANCEL.cs             CANCEL command (cancel jobs)
│           ├── RESTART.cs            RESTART command (restart jobs)
│           ├── PAUSE.cs              PAUSE command (pause jobs)
│           ├── RESUME.cs             RESUME command (resume jobs)
│           ├── JsonCommandBase.cs    Command base class (JSON deserialization)
│           └── JsonPackageConverter.cs  WS packet → StringPackageInfo
│
├── easyprint-js/                     TypeScript client SDK
│   ├── src/
│   │   ├── types.ts                  Type definitions
│   │   ├── client.ts                 EasyPrintClient implementation
│   │   └── index.ts                  Public exports
│   ├── dist/                         Build output (ESM + CJS + .d.ts)
│   ├── package.json
│   ├── tsconfig.json
│   └── tsup.config.ts
│
├── README.md                         This document (English)
├── README.zh.md                      中文文档
└── .gitignore
```
