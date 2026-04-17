# EasyPrint — 本地 HTML 标签静默打印服务

EasyPrint 是一套运行在 Windows 本机的**标签打印解决方案**，由两个子项目组成：

| 子项目 | 目录 | 语言 | 职责 |
|--------|------|------|------|
| **服务端** | `src/EasyPrint/` | C# .NET 8 WinForms | WebSocket 服务 + 渲染 + 打印 |
| **客户端 SDK** | `easyprint-js/` | TypeScript | 浏览器端 WebSocket 封装 |

---

## 目录

- [整体架构](#整体架构)
- [服务端原理](#服务端原理)
  - [启动流程](#启动流程)
  - [WebSocket 服务与消息协议](#websocket-服务与消息协议)
  - [打印流程详解](#打印流程详解)
  - [打印机信息与队列管理](#打印机信息与队列管理)
  - [UI 监控面板](#ui-监控面板)
  - [配置文件](#配置文件)
  - [依赖库](#依赖库)
- [客户端 SDK 原理](#客户端-sdk-原理)
  - [连接与重连](#连接与重连)
  - [消息队列](#消息队列)
  - [事件系统](#事件系统)
- [完整数据流](#完整数据流)
- [快速开始](#快速开始)
  - [运行服务端](#运行服务端)
  - [使用 SDK](#使用-sdk)
  - [在 Angular 中集成](#在-angular-中集成)
- [消息协议参考](#消息协议参考)
- [项目结构](#项目结构)

---

## 整体架构

```
┌─────────────────────────────────────────────────┐
│  浏览器 / Angular App                            │
│                                                  │
│  ┌──────────────────────────────────────────┐   │
│  │  @easyprint/js  (easyprint-js SDK)       │   │
│  │  EasyPrintClient                         │   │
│  │  · WebSocket 连接管理                    │   │
│  │  · 指数退避自动重连                      │   │
│  │  · 断线消息队列                          │   │
│  │  · 事件驱动 API                          │   │
│  └──────────────┬───────────────────────────┘   │
│                 │ ws://127.0.0.1:8765            │
└─────────────────┼───────────────────────────────┘
                  │ WebSocket (TCP)
┌─────────────────┼───────────────────────────────┐
│  EasyPrint 服务端 (Windows WinForms)             │
│                 │                                │
│  SuperSocket WebSocket Server                    │
│        │                                         │
│  JsonPackageConverter                            │
│  "PRINT {...}" → StringPackageInfo               │
│        │                                         │
│  命令路由（PRINT / LIST / JOBS / CANCEL / ...）  │
│        │                                         │
│  ┌─────┴──────────────────────────────┐          │
│  │  PuppeteerSharp (Chromium)         │          │
│  │  HTML → PDF (精确 mm 尺寸)         │          │
│  └─────┬──────────────────────────────┘          │
│        │                                         │
│  ┌─────┴──────────────────────────────┐          │
│  │  PDFtoImage (SkiaSharp)            │          │
│  │  PDF → Bitmap (打印机原生 DPI)     │          │
│  └─────┬──────────────────────────────┘          │
│        │                                         │
│  ┌─────┴──────────────────────────────┐          │
│  │  System.Drawing.Printing           │          │
│  │  PrintDocument → 打印机驱动        │          │
│  └─────┬──────────────────────────────┘          │
│        │                                         │
│  PrinterHelper (WinSpool P/Invoke)               │
│  · 读取打印机纸张尺寸 (<1ms)                     │
│  · 读取打印机原生 DPI                            │
│  · 枚举 / 控制打印队列任务                       │
│                                                  │
│  PrintSpoolerWatcher                             │
│  · FindFirstPrinterChangeNotification            │
│  · 事件驱动实时同步队列（无轮询）                │
│                                                  │
│  Form1 (WinForms UI)                             │
│  · 实时打印队列（Windows Spooler 同步）          │
│  · 实时日志面板                                  │
│  · 服务配置（IP/端口）                           │
└──────────────────────────────────────────────────┘
                  │
           物理打印机（USB/网络）
```

---

## 服务端原理

### 启动流程

```
Program.Main()
  │
  ├─ AppDataContext.LoadConfig()          读取 cfg.json（IP、端口、AutoStart）
  │
  ├─ WebSocketHostBuilder.Create()        构建 SuperSocket WebSocket 宿主
  │    ├─ UseSession<EasyPrintSession>()  注册自定义会话
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
  │    ├─ SetupDataGrid()                 绑定 BindingList<PrintQueueJob>
  │    ├─ DownloadBrowserAsync()          后台下载 ChromeHeadlessShell
  │    └─ OnShown
  │         ├─ StartService()             启动 WebSocket 服务
  │         ├─ LaunchAsync()             启动 Chromium
  │         ├─ SyncPrintQueue()          首次全量加载打印队列
  │         └─ PrintSpoolerWatcher.Start() 启动事件驱动队列监听
  │
  └─ Application.Run(form)               进入 WinForms 消息循环
```

### WebSocket 服务与消息协议

服务端使用 **SuperSocket 2.x** 框架，核心在 `JsonPackageConverter`：

```csharp
// JsonPackageConverter.cs
public StringPackageInfo Map(WebSocketPackage package)
{
    var arr = package.Message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    pack.Key        = arr[0];                // "PRINT" / "JOBS" / ...
    pack.Parameters = arr.Skip(1).ToArray(); // ["{...}"]（无 JSON 体时为空）
}
```

`Split(' ', 2)` 只在**第一个空格**处拆分，因此 HTML 内容中的空格完全安全。

### 打印流程详解

`PRINT` 命令处理器（`Command/PRINT.cs`）按以下步骤执行：

```
① 接收 PrintJob
   ├─ PrinterName 为空 → 调用 PrinterHelper.GetDefaultPrinterName() 自动填充
   └─ WidthMm/HeightMm 为 0 → 调用 PrinterHelper.GetDefaultPaperSizeMm() 自动读取

② 追加到 UI 打印历史（WorkForm.AddPrintJob）

③ HTML → PDF（PuppeteerSharp）
   ├─ Width/Height = 精确毫米尺寸
   ├─ PrintBackground = true    保留背景色
   └─ MarginOptions = 0         零边距

④ PDF → Bitmap（PDFtoImage + SkiaSharp）
   └─ 按打印机原生 DPI 光栅化

⑤ Bitmap → 打印机（System.Drawing.Printing）
   └─ 自适应 DPI 缩放 → 居中绘制 → 发送到打印机驱动

⑥ 发送响应消息给客户端
```

### 打印机信息与队列管理

`PrinterHelper` 通过 **WinSpool Win32 P/Invoke** 直接读取打印机驱动配置和控制打印队列：

**纸张与 DPI 读取（<1ms）：**

```
OpenPrinter() → DocumentProperties() → DEVMODE 解析
  dmPaperWidth/Length → 纸张尺寸（0.1mm 单位）
  dmPrintQuality/dmYResolution → 打印机原生 DPI
```

**打印队列操作（EnumJobs / SetJob）：**

| 方法 | 说明 |
|------|------|
| `GetPrintJobs(printerName?)` | 枚举指定打印机队列中的所有任务 |
| `CancelPrintJob(printer, jobId)` | 取消（删除）指定任务 |
| `RestartPrintJob(printer, jobId)` | 重启指定任务（从头重打） |
| `PausePrintJob(printer, jobId)` | 暂停指定任务 |
| `ResumePrintJob(printer, jobId)` | 继续已暂停的任务 |

**事件驱动队列监听（`PrintSpoolerWatcher`）：**

使用 `FindFirstPrinterChangeNotification` + `WaitForMultipleObjects` 为每台已安装的打印机注册任务变化事件。当任务新增（`ADD_JOB`）、状态变化（`SET_JOB`）或删除（`DELETE_JOB`）时，后台线程立即通知 UI 同步，无需轮询。

### UI 监控面板

`Form1` 提供以下功能：

- **服务配置**：监听 IP、端口，启动/停止服务，开机自启开关
- **Chromium 下载**：首次启动后台下载 ChromeHeadlessShell，状态栏动画显示进度
- **打印队列面板**：
  - 数据源：`BindingList<PrintQueueJob>`，实时显示 Windows 打印队列
  - `PrintQueueJob` 实现 `INotifyPropertyChanged`，状态变化自动刷新对应行
  - 列信息：队列 ID / 打印机 / 文档 / 用户 / 页数 / 状态
  - 筛选：全部 / 打印中 / 错误
  - 操作按钮：⏸ 全部暂停 / ▶ 全部继续 / ✕ 全部取消 / ⚠ 清空任务
- **运行日志**：深色终端风格 `RichTextBox`，按日志级别彩色显示

### 配置文件

配置存储于 `{exe目录}/data/cfg.json`，首次运行自动生成：

```json
{
  "Ip":                "Any",
  "Port":              8765,
  "AutoStart":         true,
  "MaxPackageLength":  1022886006,
  "ReceiveBufferSize": 409600
}
```

| 字段 | 说明 |
|------|------|
| `Ip` | 监听地址，`"Any"` 表示所有网卡 |
| `Port` | 监听端口 |
| `AutoStart` | 窗口显示后是否自动启动 WebSocket 服务 |
| `MaxPackageLength` | 最大消息包大小（字节），约 1GB，支持超大 HTML |
| `ReceiveBufferSize` | 接收缓冲区大小 |

### 依赖库

| 库 | 版本 | 用途 |
|----|------|------|
| SuperSocket | 2.0.2 | WebSocket 服务器框架 |
| SuperSocket.WebSocket.Server | 2.0.2 | WebSocket 协议支持 |
| PuppeteerSharp | 24.40.0 | Chromium 控制（HTML→PDF）|
| PDFtoImage | 5.2.0 | PDF→SKBitmap 光栅化（基于 PDFium）|
| SkiaSharp | — | 图像处理（由 PDFtoImage 引入）|
| ReaLTaiizor | 3.8.1.4 | WinForms 暗色 UI 组件库 |
| Newtonsoft.Json | 13.0.3 | JSON 序列化 |

---

## 客户端 SDK 原理

### 连接与重连

`EasyPrintClient` 构造时立即发起连接，连接生命周期：

```
constructor()
  └─ connect()
       ├─ new WebSocket(url)
       ├─ onopen  → setState('connected')  +  冲刷 pendingQueue
       ├─ onerror → setState('error')      （onclose 随后触发）
       └─ onclose → setState('disconnected')
                      └─ code !== 1000 && autoReconnect
                           └─ scheduleReconnect()
                                └─ setTimeout(connect, currentDelay)
                                     └─ currentDelay = min(delay*2, maxDelay)
```

**指数退避序列（默认配置）：**

```
第1次断线 → 等 1s  → 重连
第2次断线 → 等 2s  → 重连
第3次断线 → 等 4s  → 重连
…
第6次起   → 等 30s → 重连（封顶）
重连成功  → 重置为 1s
```

### 消息队列

当 WebSocket 未连接时调用任意发送方法，消息进入 `pendingQueue`：

```
send() → [已连接] → ws.send(message)
       → [未连接] → pendingQueue.push(message)
                       emit('queued', message, queueLength)

onopen → while (pendingQueue.length > 0)
           ws.send(pendingQueue.shift())  ← 按入队顺序重发，保证顺序
```

### 事件系统

内置轻量 `TypedEmitter`（无 Node.js `EventEmitter` 依赖，纯浏览器可用）：

| 事件 | 触发时机 | 参数 |
|------|----------|------|
| `connecting` | 开始建立连接 | — |
| `connected` | 连接成功，队列已冲刷 | — |
| `disconnected` | 连接断开 | `code, reason` |
| `error` | WebSocket 底层错误 | `Event` |
| `response` | 收到服务端响应（所有命令） | `PrintResponse` |
| `stateChange` | 状态变化 | `ConnectionState` |
| `queued` | 消息因断线入队 | `message, queueLength` |
| `reconnecting` | 正在等待重连 | `delay, attempt` |

---

## 完整数据流

```
浏览器
  │  client.print({ context: '<html>W0100025</html>', widthMm: 76, heightMm: 130 })
  │  WebSocket 帧：PRINT {"printerName":"","context":"...","widthMm":76,"heightMm":130}
  ▼
SuperSocket → JsonPackageConverter → PRINT 命令处理器
  ├─ HTML → PDF → Bitmap → 打印机驱动
  └─ 响应：{"command":"PRINT","status":200,"data":"A3F1B2C0"}

  │  client.jobs('TSC TE200')
  │  WebSocket 帧：JOBS {"printerName":"TSC TE200"}
  ▼
JOBS 命令处理器
  ├─ PrinterHelper.GetPrintJobs('TSC TE200')
  └─ 响应：{"command":"JOBS","status":200,"data":[{"jobId":1,...}]}

  │  client.cancel([1,2], 'TSC TE200')
  │  WebSocket 帧：CANCEL {"printerName":"TSC TE200","jobIds":[1,2]}
  ▼
CANCEL 命令处理器
  ├─ PrinterHelper.CancelPrintJob('TSC TE200', 1)
  ├─ PrinterHelper.CancelPrintJob('TSC TE200', 2)
  └─ 响应：{"command":"CANCEL","status":200,"data":[{"jobId":1,"ok":true},{"jobId":2,"ok":true}]}
```

---

## 快速开始

### 运行服务端

**环境要求：** Windows 10/11，.NET 8 SDK，支持 Windows GDI 打印

```powershell
cd src/EasyPrint

# 首次运行：自动下载 ChromeHeadlessShell（约 50MB，仅需一次）
dotnet run

# 发布为独立可执行文件（无需安装 .NET 运行时）
dotnet publish -c Release -r win-x64 --self-contained true
```

首次启动后，配置文件自动生成于 `{exe目录}/data/cfg.json`。

修改 `Ip` 和 `Port` 后重启服务生效，或直接在 UI 界面修改并点击"启动服务"。

---

### 使用 SDK

**安装：**

```bash
npm install @easyprint/js
```

**基本用法：**

```typescript
import { EasyPrintClient, PrintQueueJob, JobControlResult } from '@easyprint/js';

const client = new EasyPrintClient({ host: '127.0.0.1', port: 8765 });

client.on('connected',    () => console.log('已连接'));
client.on('disconnected', (code) => console.log('断线', code));

client.on('response', resp => {
  if (resp.command === 'PRINT' && resp.status === 200) {
    console.log('打印成功，任务 ID:', resp.data);
  }
  if (resp.command === 'JOBS' && resp.status === 200) {
    const jobs = resp.data as PrintQueueJob[];
    console.log('当前队列:', jobs);
  }
  if (resp.command === 'CANCEL') {
    const results = resp.data as JobControlResult[];
    results.forEach(r => console.log(`任务 ${r.jobId}：${r.ok ? '已取消' : '失败'}`));
  }
});

// 打印
client.print({ context: '<html><body>W0100025</body></html>', widthMm: 76, heightMm: 130 });

// 查询队列
client.jobs('TSC TE200');

// 暂停 / 继续 / 取消任务
client.pause([1, 2], 'TSC TE200');
client.resume([1, 2], 'TSC TE200');
client.cancel([3], 'TSC TE200');
```

---

### 在 Angular 中集成

**environment.ts：**

```typescript
export const environment = {
  easyPrint: { host: '127.0.0.1', port: 8765 },
};
```

**print.service.ts：**

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

## 消息协议参考

所有命令遵循统一格式：`命令 <JSON 体>` 或纯命令字符串（无需 JSON 体时）。

### 命令总览

| 命令 | 发送格式 | 响应 `data` 类型 | 说明 |
|------|----------|-----------------|------|
| `PRINT` | `PRINT {PrintJob}` | `string`（任务 ID） | 打印 HTML |
| `LIST` | `LIST` | `PrinterInfo[]` | 枚举已安装打印机 |
| `JOBS` | `JOBS {printerName?}` | `PrintQueueJob[]` | 查询打印队列 |
| `CANCEL` | `CANCEL {printerName?, jobIds[]}` | `JobControlResult[]` | 取消任务 |
| `RESTART` | `RESTART {printerName?, jobIds[]}` | `JobControlResult[]` | 重启任务 |
| `PAUSE` | `PAUSE {printerName?, jobIds[]}` | `JobControlResult[]` | 暂停任务 |
| `RESUME` | `RESUME {printerName?, jobIds[]}` | `JobControlResult[]` | 继续任务 |

### 统一响应格式

```json
{
  "command": "PRINT",
  "status":  200,
  "message": "打印成功",
  "data":    "A3F1B2C0"
}
```

| 字段 | 说明 |
|------|------|
| `command` | 对应的命令名称 |
| `status` | `200` 成功 / `207` 部分成功 / `400` 请求错误 / `500` 服务器异常 |
| `message` | 可读的结果描述 |
| `data` | 响应数据，类型因命令而异 |

### PRINT 请求字段

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `printerName` | string | `""` | 打印机名称，空字符串使用系统默认打印机 |
| `context` | string | **必填** | 要打印的完整 HTML 字符串 |
| `widthMm` | number | `0` | 标签宽度（毫米），0 = 自动读取打印机配置 |
| `heightMm` | number | `0` | 标签高度（毫米），0 = 自动读取打印机配置 |
| `paddingMm` | number[] | `[0,0,0,0]` | 页面填充，单位毫米，顺序上右下左 |

### 批量控制命令请求字段（CANCEL / RESTART / PAUSE / RESUME）

| 字段 | 类型 | 说明 |
|------|------|------|
| `printerName` | string? | 打印机名称，空字符串使用默认打印机 |
| `jobIds` | number[] | 要操作的任务 ID 列表，支持单个或多个 |

---

## 项目结构

```
EasyPrint/
├── src/
│   └── EasyPrint/                    C# WinForms 服务端
│       ├── Program.cs                入口，注册所有命令并启动
│       ├── AppSettings.cs            配置模型（IP/端口/AutoStart）
│       ├── AppDataContext.cs         配置文件读写
│       ├── EasyPrintService.cs       SuperSocket 服务
│       ├── EasyPrintSession.cs       WebSocket 会话（SendMessage）
│       ├── PrintJob.cs               EasyPrint 打印任务模型
│       ├── PrintJobStatus.cs         任务状态枚举
│       ├── PrintQueueJob.cs          Windows 队列任务模型（INotifyPropertyChanged）
│       ├── PrintResponseMessage.cs   响应消息模型
│       ├── PrinterHelper.cs          WinSpool P/Invoke（纸张/DPI/队列读写）
│       ├── PrintSpoolerWatcher.cs    事件驱动打印队列监听
│       ├── UiLogger.cs               日志路由到 UI 面板
│       ├── Form1.cs                  主窗口业务逻辑
│       ├── Form1.Designer.cs         WinForms 暗色 UI 布局
│       └── Command/
│           ├── PRINT.cs              PRINT 命令（HTML→PDF→打印）
│           ├── LIST.cs               LIST 命令（枚举打印机）
│           ├── JOBS.cs               JOBS 命令（查询打印队列）
│           ├── CANCEL.cs             CANCEL 命令（取消任务）
│           ├── RESTART.cs            RESTART 命令（重启任务）
│           ├── PAUSE.cs              PAUSE 命令（暂停任务）
│           ├── RESUME.cs             RESUME 命令（继续任务）
│           ├── JsonCommandBase.cs    命令基类（JSON 反序列化）
│           └── JsonPackageConverter.cs  WebSocket包→StringPackageInfo
│
├── easyprint-js/                     TypeScript 客户端 SDK
│   ├── src/
│   │   ├── types.ts                  类型定义（PrintJobRequest / PrintQueueJob / ...）
│   │   ├── client.ts                 EasyPrintClient 实现
│   │   └── index.ts                  公开导出
│   ├── dist/                         构建产物（ESM + CJS + .d.ts）
│   ├── package.json                  npm 包配置
│   ├── tsconfig.json                 TypeScript 配置
│   └── tsup.config.ts                构建工具配置
│
├── .gitignore                        过滤 bin/obj/node_modules/dist
└── README.md                         本文档
```
