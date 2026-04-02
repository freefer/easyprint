# EasyPrint — 本地 HTML 标签静默打印服务

EasyPrint 是一套运行在 Windows 本机的**标签打印解决方案**，由两个子项目组成：

| 子项目 | 目录 | 语言 | 职责 |
|---|---|---|---|
| **服务端** | `src/EasyPrint/` | C# .NET 8 WinForms | WebSocket 服务 + 渲染 + 打印 |
| **客户端 SDK** | `easyprint-js/` | TypeScript | 浏览器端 WebSocket 封装 |

---

## 目录

- [整体架构](#整体架构)
- [服务端原理](#服务端原理)
  - [启动流程](#启动流程)
  - [WebSocket 服务与消息协议](#websocket-服务与消息协议)
  - [打印流程详解](#打印流程详解)
  - [打印机信息读取](#打印机信息读取)
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
│  │  @chenghf/easyprint  (easyprint-js SDK)  │   │
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
│  "PRINT {...}" → StringPackageInfo              │
│        │                                         │
│  PRINT Command Handler                           │
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
│  │  PrintDocument → 打印机驱动         │          │
│  └─────┬──────────────────────────────┘          │
│        │                                         │
│  PrinterHelper (WinSpool P/Invoke)               │
│  · 读取打印机纸张尺寸 (<1ms)                      │
│  · 读取打印机原生 DPI                             │
│                                                  │
│  Form1 (WinForms UI)                             │
│  · 打印任务历史（BindingList 双向绑定）           │
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
  │    ├─ ConfigureSuperSocket()          绑定监听 IP:Port
  │    ├─ UseSession<EasyPrintSession>()  注册自定义会话（含 SendMessage 方法）
  │    ├─ UseHostedService<EasyPrintService>()  注册服务（持有 Browser/WorkForm 引用）
  │    ├─ UseCommand<StringPackageInfo, JsonPackageConverter>()
  │    │    └─ AddCommand<PRINT>()        注册 PRINT 命令处理器
  │    └─ ConfigureLogging → UiLoggerProvider  日志路由到 UI 面板
  │
  ├─ new Form1(cfg, host, loggerProvider) 创建 UI 窗口
  │    ├─ InitializeComponent()           初始化 WinForms 控件
  │    ├─ DownloadBrowserAsync()          后台下载 ChromeHeadlessShell（已缓存则跳过）
  │    └─ OnShown → PuppeteerSharp.LaunchAsync()  启动 Chromium 浏览器进程
  │
  └─ Application.Run(form)               进入 WinForms 消息循环
```

### WebSocket 服务与消息协议

服务端使用 **SuperSocket 2.x** 框架，核心在 `JsonPackageConverter`：

```csharp
// JsonPackageConverter.cs
public StringPackageInfo Map(WebSocketPackage package)
{
    // "PRINT {\"printerName\":\"TSC\",\"context\":\"<html>...\"}"
    var arr = package.Message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    pack.Key        = arr[0];           // "PRINT"
    pack.Parameters = arr.Skip(1).ToArray(); // ["{\"printerName\":...}"]
}
```

`Split(' ', 2)` 只在**第一个空格**处拆分，因此 HTML 内容中的空格完全安全。

`JsonCommandBase<T>` 通过 `package.Parameters[0]` 获取 JSON 体，反序列化为目标类型：

```csharp
// JsonCommandBase.cs
var context = package.Parameters[0];           // 完整 JSON 字符串
var data    = JsonConvert.DeserializeObject<T>(context);  // 反序列化为 PrintJob
```

**消息格式总结：**

```
客户端发送（文本帧）：
  PRINT {"printerName":"TSC TE200","context":"<html>...</html>","widthMm":76,"heightMm":130}
  ─┬──── ────────────────────────────────────────────────────────────────────────────────
   │     Parameters[0] = 完整 JSON，可包含任意空格
   Key = "PRINT" → 路由到 PRINT 命令处理器

服务端响应（文本帧）：
  {"command":"PRINT","status":200,"message":"打印成功","data":"A3F1B2C0"}
  │                  │            │                   │
  │                  │            │                   └─ 服务端生成的任务 ID
  │                  │            └─ 可读描述
  │                  └─ 200=成功 / 400=错误 / 500=服务器异常
  └─ 响应对应的命令名称
```

### 打印流程详解

`PRINT` 命令处理器（`Command/PRINT.cs`）按以下步骤执行：

```
① 接收 PrintJob
   ├─ PrinterName 为空 → 调用 PrinterHelper.GetDefaultPrinterName() 自动填充
   └─ WidthMm/HeightMm 为 0 → 调用 PrinterHelper.GetDefaultPaperSizeMm() 自动读取

② 追加到 UI 打印历史（WorkForm.AddPrintJob）
   └─ BindingList<PrintJob> 自动刷新 DataGridView

③ HTML → PDF（PuppeteerSharp）
   ├─ browser.NewPageAsync()         新建 Chromium 标签页
   ├─ page.SetContentAsync(html)     注入 HTML 内容
   └─ page.PdfStreamAsync(PdfOptions)
        ├─ Width  = "{WidthMm}mm"    精确物理尺寸（毫米）
        ├─ Height = "{HeightMm}mm"
        ├─ PrintBackground = true    保留背景色（条码/底色必须）
        └─ MarginOptions = 0         零边距，充分利用纸张

④ PDF → Bitmap（PDFtoImage + SkiaSharp）
   ├─ Conversion.ToImages(pdfData)   PDFium 光栅化为 SKBitmap
   ├─ SKImage.FromBitmap → Encode(PNG, 100)  高质量无损编码
   └─ new Bitmap(stream)             转为 GDI Bitmap

⑤ Bitmap → 打印机（System.Drawing.Printing）
   ├─ PrintDocument.PrinterSettings.PrinterName  指定打印机
   ├─ DefaultPageSettings.Margins = 0            零边距
   └─ PrintPage 事件：
        ├─ bitmapDpi  = bitmap.HorizontalResolution（渲染分辨率）
        ├─ printerDpi = e.Graphics.DpiX           （打印机原生 DPI）
        ├─ scale      = printerDpi / bitmapDpi    （自适应缩放比）
        └─ Graphics.DrawImage(bitmap, destRect)   （居中输出）

⑥ 更新任务状态
   ├─ 成功 → Status = Completed（PropertyChanged 自动刷新 UI 行）
   ├─ 失败 → Status = Failed
   └─ 发送响应消息给客户端
```

### 打印机信息读取

`PrinterHelper` 通过 **WinSpool Win32 P/Invoke** 直接读取打印机驱动配置，避免 `System.Drawing.Printing.PrinterSettings.PaperSizes` 枚举（通常耗时 100–500ms）：

```
GetDefaultPrinter()     → 打印机名称（<1ms）
  ↓
OpenPrinter()           → 获取打印机句柄
  ↓
DocumentProperties()    → 读取 DEVMODE 结构体（单次系统调用）
  ↓
DEVMODE 解析：
  dmPaperWidth  (offset 82)  → 纸宽（0.1mm 单位）
  dmPaperLength (offset 80)  → 纸高（0.1mm 单位）
  dmOrientation (offset 76)  → 1=纵向 / 2=横向（横向则交换宽高）
  dmPrintQuality(offset 90)  → x-DPI（>0 为实际值，<0 为枚举）
  dmYResolution (offset 96)  → y-DPI（首选，更精确）
  ↓
ClosePrinter()          → 释放句柄
```

**纸张尺寸优先级：**
1. `dmPaperWidth/Length > 0` → 使用精确值（自定义纸张 / 现代驱动）
2. 否则按 `dmPaperSize` 代码查内置表（Letter/A4/A5 等标准纸型）
3. 查找失败 → 回退 A4 (210 × 297mm)

### UI 监控面板

`Form1` 提供以下功能：

- **服务配置**：监听 IP、端口，启动/停止服务按钮
- **Chromium 下载**：首次启动后台下载 ChromeHeadlessShell，状态栏动画显示进度
- **打印任务历史**：
  - 数据源：`BindingList<PrintJob>` 双向绑定 DataGridView
  - `PrintJob` 实现 `INotifyPropertyChanged`，`Status` 变化自动刷新对应行，无需手动重建列表
  - 支持按状态筛选（全部 / 待处理 / 失败）
  - 支持重试失败任务、清除已完成记录
- **运行日志**：深色终端风格 `RichTextBox`，按日志级别彩色显示，`UiLoggerProvider` 将 SuperSocket 内部日志路由到此面板

### 配置文件

配置存储于 `{exe目录}/data/cfg.json`，首次运行自动生成：

```json
{
  "Ip":                "Any",
  "Port":              201212,
  "AutoStart":         true,
  "MaxPackageLength":  1022886006,
  "ReceiveBufferSize": 409600
}
```

| 字段 | 说明 |
|---|---|
| `Ip` | 监听地址，`"Any"` 表示所有网卡 |
| `Port` | 监听端口 |
| `AutoStart` | 窗口显示后是否自动启动 WebSocket 服务 |
| `MaxPackageLength` | 最大消息包大小（字节），约 1GB，支持超大 HTML |
| `ReceiveBufferSize` | 接收缓冲区大小 |

### 依赖库

| 库 | 版本 | 用途 |
|---|---|---|
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
第1次断线 → 等 1s → 重连
第2次断线 → 等 2s → 重连
第3次断线 → 等 4s → 重连
第4次断线 → 等 8s → 重连
第5次断线 → 等 16s → 重连
第6次起   → 等 30s → 重连（封顶）
重连成功  → 重置为 1s
```

**连接状态机：**

```
           connect()
              │
         ┌────▼─────┐
         │connecting│
         └────┬─────┘
    onopen ───┘   onclose/onerror
              │          │
         ┌────▼─────┐    │
         │connected │    │
         └────┬─────┘    │
    onclose───┘  ┌───────▼──────┐    ┌────────────┐
                 │disconnected  │───►│ reconnecting│
                 └──────────────┘    └────────────┘
              onerror
         ┌────▼─────┐
         │  error   │──► onclose 接管
         └──────────┘
```

### 消息队列

当 WebSocket 未连接时调用 `print()`，消息进入 `pendingQueue`：

```
print() → [已连接] → ws.send(message)
       → [未连接] → pendingQueue.push(message)
                       emit('queued', message, queueLength)

onopen → while (pendingQueue.length > 0)
           ws.send(pendingQueue.shift())  ← 按入队顺序重发，保证顺序
```

### 事件系统

内置轻量 `TypedEmitter`（无 Node.js `EventEmitter` 依赖，纯浏览器可用）：

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `connecting` | 开始建立连接 | — |
| `connected` | 连接成功，队列已冲刷 | — |
| `disconnected` | 连接断开 | `code, reason` |
| `error` | WebSocket 底层错误 | `Event` |
| `response` | 收到服务端响应 | `PrintResponse` |
| `stateChange` | 状态变化 | `ConnectionState` |
| `queued` | 消息因断线入队 | `message, queueLength` |
| `reconnecting` | 正在等待重连 | `delay, attempt` |

---

## 完整数据流

```
浏览器
  │
  │  client.print({ context: '<html>W0100025</html>', widthMm: 76, heightMm: 130 })
  │
  │  WebSocket 文本帧：
  │  PRINT {"printerName":"","context":"<html>W0100025</html>","widthMm":76,"heightMm":130}
  │
  ▼
SuperSocket (WebSocket Server)
  │
  │  JsonPackageConverter.Map()
  │  Split(' ', 2) → Key="PRINT", Parameters[0]="{...}"
  │
  ▼
PRINT Command Handler
  │
  ├─ PrinterHelper.GetDefaultPrinterName()    → "TSC TE200"   (<1ms)
  ├─ PrinterHelper.GetDefaultPaperSizeMm()   → (76.0, 130.0) (<1ms)
  │
  ├─ WorkForm.AddPrintJob(job)               → UI 历史表格新增一行
  │
  ├─ PuppeteerSharp.Page.PdfStreamAsync()
  │    HTML → PDF  (Width=76mm, Height=130mm, 零边距)
  │
  ├─ PDFtoImage.Conversion.ToImages()
  │    PDF → SKBitmap（打印机原生 DPI 光栅化）
  │
  ├─ SKImage.Encode(PNG, 100) → new Bitmap(stream)
  │
  ├─ PrintDocument.Print()
  │    自适应 DPI 缩放 → 居中绘制 → 发送到打印机驱动
  │
  ├─ WorkForm.UpdatePrintJob(id, Completed)  → UI 行状态自动刷新
  │
  └─ session.SendMessage({ status:200, message:"打印成功", data:"任务ID" })
       │
       ▼
  WebSocket 文本帧：
  {"command":"PRINT","status":200,"message":"打印成功","data":"A3F1B2C0"}
       │
       ▼
  client.on('response', resp => { ... })
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

首次启动后，配置文件自动生成于：
```
{exe目录}/data/cfg.json
```

修改 `Ip` 和 `Port` 后重启服务生效，或直接在 UI 界面修改并点击"启动服务"。

---

### 使用 SDK

**安装：**
```bash
npm install @chenghf/easyprint
```

**基本用法：**
```typescript
import { EasyPrintClient } from '@chenghf/easyprint';

const client = new EasyPrintClient({
  host: '127.0.0.1',
  port: 201212,
});

// 监听连接状态
client.on('connected',    ()           => console.log('✅ 已连接'));
client.on('disconnected', (code)       => console.log('⚠️ 断线', code));
client.on('reconnecting', (delay, n)   => console.log(`🔄 第${n}次重连，等待${delay}ms`));
client.on('queued',       (msg, total) => console.log(`📥 入队（共${total}条待发）`));

// 监听打印结果
client.on('response', resp => {
  if (resp.status === 200) {
    console.log('打印成功，任务 ID:', resp.data);
  } else {
    console.error('打印失败:', resp.message);
  }
});

// 发送打印指令（断线时自动入队，重连后自动重发）
client.print({
  context:     '<html><body style="margin:0">W0100025</body></html>',
  widthMm:     76,
  heightMm:    130,
  printerName: 'TSC TE200',  // 留空则使用默认打印机
});
```

---

### 在 Angular 中集成

**environment.ts：**
```typescript
export const environment = {
  easyPrint: {
    host: '127.0.0.1',
    port: 201212,
  },
  // ...
};
```

**print.service.ts：**
```typescript
import { Injectable, OnDestroy } from '@angular/core';
import { Observable } from 'rxjs';
import { EasyPrintClient, PrintResponse } from '@chenghf/easyprint';
import { environment } from 'src/environments/environment';

@Injectable({ providedIn: 'root' })
export class PrintService implements OnDestroy {
  private readonly client = new EasyPrintClient(environment.easyPrint);

  /** 响应消息流（可在组件中订阅） */
  readonly response$ = new Observable<PrintResponse>(observer => {
    const handler = (resp: PrintResponse) => observer.next(resp);
    this.client.on('response', handler);
    return () => this.client.off('response', handler);
  });

  /** 打印标签 */
  printLabel(html: string, widthMm = 76, heightMm = 130): void {
    this.client.print({ context: html, widthMm, heightMm });
  }

  ngOnDestroy(): void {
    this.client.destroy();
  }
}
```

---

## 消息协议参考

### 客户端 → 服务端

```
PRINT <PrintJob JSON>
```

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `printerName` | string | `""` | 打印机名称，空字符串使用系统默认打印机 |
| `context` | string | **必填** | 要打印的完整 HTML 字符串 |
| `widthMm` | number | `0` | 标签宽度（毫米），0 = 自动读取打印机配置 |
| `heightMm` | number | `0` | 标签高度（毫米），0 = 自动读取打印机配置 |

**示例：**
```
PRINT {"printerName":"TSC TE200","context":"<html><body>W0100025</body></html>","widthMm":76,"heightMm":130}
```

### 服务端 → 客户端

```json
{
  "command": "PRINT",
  "status":  200,
  "message": "打印成功",
  "data":    "A3F1B2C0"
}
```

| 字段 | 说明 |
|---|---|
| `command` | 对应的命令名称 |
| `status` | `200` 成功 / `400` 请求错误 / `500` 服务器异常 |
| `message` | 可读的结果描述 |
| `data` | 服务端生成的任务 ID（8 位大写十六进制） |

---

## 项目结构

```
EasyPrint/
├── src/
│   └── EasyPrint/               C# WinForms 服务端
│       ├── Program.cs           入口，构建 SuperSocket 宿主
│       ├── AppSettings.cs       配置模型（IP/端口/AutoStart）
│       ├── AppDataContext.cs    配置文件读写
│       ├── EasyPrintService.cs  SuperSocket 服务（持有 Browser/WorkForm）
│       ├── EasyPrintSession.cs  WebSocket 会话（含 SendMessage）
│       ├── PrintJob.cs          打印任务模型（INotifyPropertyChanged）
│       ├── PrintJobStatus.cs    任务状态枚举
│       ├── PrintResponseMessage.cs  响应消息模型
│       ├── PrinterHelper.cs     WinSpool P/Invoke（纸张尺寸/DPI）
│       ├── UiLogger.cs          日志路由到 UI 面板
│       ├── Form1.cs             主窗口业务逻辑
│       ├── Form1.Designer.cs    WinForms 暗色 UI 布局
│       └── Command/
│           ├── PRINT.cs         PRINT 命令（HTML→PDF→打印）
│           ├── JsonCommandBase.cs   命令基类（JSON 反序列化）
│           └── JsonPackageConverter.cs  WebSocket包→StringPackageInfo
│
├── easyprint-js/                TypeScript 客户端 SDK
│   ├── src/
│   │   ├── types.ts             类型定义
│   │   ├── client.ts            EasyPrintClient 实现
│   │   └── index.ts             公开导出
│   ├── dist/                    构建产物（ESM + CJS + .d.ts）
│   ├── package.json             npm 包配置
│   ├── tsconfig.json            TypeScript 配置
│   └── tsup.config.ts           构建工具配置
│
├── .gitignore                   过滤 bin/obj/node_modules/dist
└── README.md                    本文档
```
