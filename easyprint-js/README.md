English | [中文](./README.zh.md)

# @easyprint/js

Framework-agnostic WebSocket client SDK for the EasyPrint local printing service. Works in all modern browsers.

> **Project:** [https://github.com/freefer/easyprint](https://github.com/freefer/easyprint)

---

## Overview

EasyPrint is a **label printing solution** that runs natively on Windows, consisting of two sub-projects:

| Sub-project | Directory | Language | Role |
|-------------|-----------|----------|------|
| **Server** | `src/EasyPrint/` | C# .NET 8 WinForms | WebSocket service + rendering + printing |
| **Client SDK** | `easyprint-js/` | TypeScript | Browser-side WebSocket wrapper |

---

## Server

### Requirements

- Windows 10 / 11
- .NET 8 SDK
- Windows GDI printing support

### Running the Server

1. Download `win-x64.zip` from the [Releases page](https://github.com/freefer/easyprint/releases/tag/v1.1.0)
2. Extract and run `EasyPrint.exe`
3. On first launch, ChromeHeadlessShell (~50 MB) is downloaded automatically
4. Configure the listen address / port in the UI and click **Start Service**

The configuration file is auto-generated at `{exe_dir}/data/cfg.json`:

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
| `Ip` | Listen address; `"Any"` = all interfaces |
| `Port` | Listen port |
| `AutoStart` | Auto-start the WebSocket service on window show |
| `MaxPackageLength` | Max message size (bytes), supports large HTML payloads |
| `ReceiveBufferSize` | Receive buffer size |

### Server Dependencies

| Library | Version | Purpose |
|---------|---------|---------|
| SuperSocket | 2.0.2 | WebSocket server framework |
| SuperSocket.WebSocket.Server | 2.0.2 | WebSocket protocol support |
| PuppeteerSharp | 24.40.0 | Chromium control (HTML → PDF) |
| PDFtoImage | 5.2.0 | PDF → SKBitmap rasterization (PDFium) |
| SkiaSharp | — | Image processing (pulled in by PDFtoImage) |
| ReaLTaiizor | 3.8.1.4 | WinForms dark theme UI controls |
| Newtonsoft.Json | 13.0.3 | JSON serialization |

### Print Pipeline

```
① Receive PrintJob
   ├─ PrinterName empty → auto-resolve default printer
   └─ WidthMm/HeightMm = 0 → auto-read paper size from printer

② HTML → PDF (PuppeteerSharp / Chromium)
   └─ Exact mm dimensions, zero margins, background colors preserved

③ PDF → Bitmap (PDFtoImage + SkiaSharp)
   └─ Rasterized at native printer DPI

④ Bitmap → Printer (System.Drawing.Printing)
   └─ Adaptive DPI scaling, centered output

⑤ Return response to client
```

---

## Client SDK

### Install

```bash
npm install @easyprint/js
```

### Quick Start

```ts
import { EasyPrintClient } from '@easyprint/js';

const client = new EasyPrintClient({
  host: '127.0.0.1',
  port: 8765,
});

// Connection events
client.on('connected',    () => console.log('Connected'));
client.on('disconnected', (code) => console.log('Disconnected', code));
client.on('reconnecting', (delay, attempt) =>
  console.log(`Reconnect attempt ${attempt}, waiting ${delay}ms`));

// Handle all server responses in one place
client.on('response', resp => {
  switch (resp.command) {
    case 'PRINT':   console.log('Print job ID:', resp.data); break;
    case 'LIST':    console.log('Printers:', resp.data); break;
    case 'JOBS':    console.log('Queue jobs:', resp.data); break;
    case 'CANCEL':
    case 'RESTART':
    case 'PAUSE':
    case 'RESUME':  console.log(`${resp.command} result:`, resp.data); break;
  }
});

// Print an HTML label (queued automatically when disconnected)
client.print({
  context:     '<html><body style="margin:0"><p>W0100025</p></body></html>',
  widthMm:     76,
  heightMm:    130,
  printerName: 'TSC TE200',   // omit to use the server's default printer
});
```

### API

#### `new EasyPrintClient(options?)`

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `host` | `string` | `'127.0.0.1'` | Server IP address |
| `port` | `number` | `8765` | Server port |
| `autoReconnect` | `boolean` | `true` | Auto-reconnect on disconnect |
| `reconnectDelay` | `number` | `1000` | Initial reconnect delay (ms) |
| `maxReconnectDelay` | `number` | `30000` | Max reconnect delay (ms) |

#### Methods

| Method | Description |
|--------|-------------|
| `print(job)` | Send a print job; queued automatically when disconnected |
| `list()` | List all installed printers on the server machine |
| `jobs(printerName?)` | Query the print queue for a given printer |
| `cancel(jobIds, printerName?)` | Cancel (delete) one or more queue jobs |
| `restart(jobIds, printerName?)` | Restart one or more queue jobs from the beginning |
| `pause(jobIds, printerName?)` | Pause one or more queue jobs |
| `resume(jobIds, printerName?)` | Resume paused queue jobs |
| `send(message)` | Send a raw command frame (advanced usage) |
| `disconnect()` | Manually disconnect (still reconnects if `autoReconnect=true`) |
| `reconnectNow()` | Reconnect immediately, reset backoff delay |
| `destroy()` | Permanently destroy the client, clear all timers |

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `state` | `ConnectionState` | Current connection state snapshot |
| `isConnected` | `boolean` | Whether the client is connected |
| `pendingCount` | `number` | Number of messages in the offline queue |
| `url` | `string` | Server WebSocket URL |

#### Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `connecting` | — | Connection attempt started |
| `connected` | — | Connected, pending queue flushed |
| `disconnected` | `code, reason` | Connection closed |
| `error` | `Event` | WebSocket layer error |
| `response` | `PrintResponse` | Server response received (all commands) |
| `stateChange` | `ConnectionState` | Connection state changed |
| `queued` | `message, length` | Message queued due to disconnect |
| `reconnecting` | `delay, attempt` | Waiting to reconnect |

---

### `PrintJobRequest`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `context` | `string` | — | **Required.** HTML content to print |
| `printerName` | `string?` | `''` | Printer name; empty = server default |
| `widthMm` | `number?` | `76` | Label width in mm; `0` = auto-read from printer (slower) |
| `heightMm` | `number?` | `130` | Label height in mm; `0` = auto-read from printer (slower) |
| `paddingMm` | `number[]?` | `[0,0,0,0]` | Page padding in mm: top, right, bottom, left |

### `PrintResponse`

| Field | Type | Description |
|-------|------|-------------|
| `command` | `string` | The command that triggered this response |
| `status` | `number` | `200` success / `207` partial / `400` bad request / `500` server error |
| `message` | `string` | Human-readable result description |
| `data` | `any` | Response payload; type depends on command (see table below) |

### `data` Field by Command

| Command | `data` Type | Description |
|---------|-------------|-------------|
| `PRINT` | `string` | Server-generated job ID (8-char uppercase hex) |
| `LIST` | `PrinterInfo[]` | Installed printer list |
| `JOBS` | `PrintQueueJob[]` | Current queue job list |
| `CANCEL` / `RESTART` / `PAUSE` / `RESUME` | `JobControlResult[]` | Per-job operation result |

### `PrinterInfo`

| Field | Type | Description |
|-------|------|-------------|
| `name` | `string` | Printer name |
| `isDefault` | `boolean` | Whether this is the system default printer |

### `PrintQueueJob`

| Field | Type | Description |
|-------|------|-------------|
| `jobId` | `number` | Windows system-assigned job ID |
| `printerName` | `string` | Printer name |
| `document` | `string` | Document name |
| `userName` | `string` | User who submitted the job |
| `statusLabel` | `string` | Human-readable status, e.g. `"Printing"` / `"Completed"` / `"Error"` |
| `status` | `number` | Status bitmask (JOB_STATUS_* flags) |
| `totalPages` | `number` | Total page count (0 = unknown) |
| `pagesPrinted` | `number` | Pages printed so far |
| `priority` | `number` | Print priority (1–99) |
| `position` | `number` | Position in queue (1-based) |

### `JobControlResult`

Single element in the `data` array of batch control responses (CANCEL / RESTART / PAUSE / RESUME):

| Field | Type | Description |
|-------|------|-------------|
| `jobId` | `number` | The job that was operated on |
| `ok` | `boolean` | Whether the operation succeeded |

---

## Protocol Reference

The server uses `Split(' ', 2)` to split only at the **first space**, so HTML content with spaces is safe.

**PRINT — Submit a print job:**

```
Client → Server:
  PRINT {"printerName":"TSC TE200","context":"<html>...</html>","widthMm":76,"heightMm":130}

Server → Client:
  {"command":"PRINT","status":200,"message":"Print successful","data":"A3F1B2C0"}
```

**LIST — Get printer list:**

```
Client → Server:
  LIST

Server → Client:
  {"command":"LIST","status":200,"message":"2 printer(s)","data":[{"name":"TSC TE200","isDefault":true}]}
```

**JOBS — Query print queue:**

```
Client → Server:
  JOBS {"printerName":"TSC TE200"}   // printerName is optional
  JOBS

Server → Client:
  {"command":"JOBS","status":200,"message":"3 job(s)","data":[
    {"jobId":1,"printerName":"TSC TE200","document":"Label","userName":"Admin",
     "statusLabel":"Printing","status":16,"totalPages":1,"pagesPrinted":0,"priority":1,"position":1}
  ]}
```

**CANCEL / RESTART / PAUSE / RESUME — Batch job control:**

```
Client → Server (CANCEL example, supports multiple jobIds):
  CANCEL {"printerName":"TSC TE200","jobIds":[5,6,7]}

Server → Client:
  {"command":"CANCEL","status":200,"message":"3 job(s): 3 succeeded, 0 failed",
   "data":[{"jobId":5,"ok":true},{"jobId":6,"ok":true},{"jobId":7,"ok":false}]}
```

> **`status` semantics:** `200` = all succeeded · `207` = partial success · `500` = all failed

---

## Angular Integration

**environment.ts:**

```ts
export const environment = {
  easyPrint: {
    host: '127.0.0.1',
    port: 8765,
  },
};
```

**print.service.ts:**

```ts
import { Injectable, OnDestroy } from '@angular/core';
import { Observable } from 'rxjs';
import { EasyPrintClient, PrintResponse, PrintQueueJob, JobControlResult } from '@easyprint/js';
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

  getQueue(printerName?: string): void {
    this.client.jobs(printerName);
  }

  cancelJobs(jobIds: number[], printerName?: string): void {
    this.client.cancel(jobIds, printerName);
  }

  pauseJobs(jobIds: number[], printerName?: string): void {
    this.client.pause(jobIds, printerName);
  }

  resumeJobs(jobIds: number[], printerName?: string): void {
    this.client.resume(jobIds, printerName);
  }

  ngOnDestroy(): void {
    this.client.destroy();
  }
}
```

---

## Reconnect Strategy

Exponential backoff, resets automatically on successful reconnect:

```
Disconnect → wait 1s → fail → wait 2s → fail → wait 4s → … → wait 30s (cap)
                                                                    ↓
                                                           Reconnect OK → reset to 1s
```

---

## Build

```bash
npm install
npm run build   # output to dist/
```

## Publish to npm

```bash
npm login
npm publish --access public
```
