[English](./README.md) | 中文

# @easyprint/js

EasyPrint 本地打印服务的 WebSocket 客户端 SDK，框架无关，支持所有主流浏览器环境。

> **项目地址：** [https://github.com/freefer/easyprint](https://github.com/freefer/easyprint)

---

## 项目概述

EasyPrint 是一套运行在 Windows 本机的**标签打印解决方案**，由两个子项目组成：

| 子项目 | 目录 | 语言 | 职责 |
|--------|------|------|------|
| **服务端** | `src/EasyPrint/` | C# .NET 8 WinForms | WebSocket 服务 + 渲染 + 打印 |
| **客户端 SDK** | `easyprint-js/` | TypeScript | 浏览器端 WebSocket 封装 |

---

## 服务端

### 环境要求

- Windows 10 / 11
- .NET 8 SDK
- 支持 Windows GDI 打印

### 运行服务端

1. 前往 [Releases 页面](https://github.com/freefer/easyprint/releases) 下载 `win-x64.zip`
2. 解压后运行 `EasyPrint.exe`
3. 首次启动会自动下载 ChromeHeadlessShell（约 50MB，仅需一次）
4. 在 UI 界面配置监听地址 / 端口，点击"启动服务"

首次启动后，配置文件自动生成于 `{exe目录}/data/cfg.json`：

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
| `MaxPackageLength` | 最大消息包大小（字节），支持超大 HTML |
| `ReceiveBufferSize` | 接收缓冲区大小 |

修改 `Ip` 和 `Port` 后重启服务生效，或直接在 UI 界面修改并点击"启动服务"。

### 服务端依赖库

| 库 | 版本 | 用途 |
|----|------|------|
| SuperSocket | 2.0.2 | WebSocket 服务器框架 |
| SuperSocket.WebSocket.Server | 2.0.2 | WebSocket 协议支持 |
| PuppeteerSharp | 24.40.0 | Chromium 控制（HTML → PDF） |
| PDFtoImage | 5.2.0 | PDF → SKBitmap 光栅化（基于 PDFium） |
| SkiaSharp | — | 图像处理（由 PDFtoImage 引入） |
| ReaLTaiizor | 3.8.1.4 | WinForms 暗色 UI 组件库 |
| Newtonsoft.Json | 13.0.3 | JSON 序列化 |

### 打印流程

服务端收到打印请求后按以下步骤执行：

```
① 接收 PrintJob
   ├─ PrinterName 为空 → 自动读取系统默认打印机
   └─ WidthMm/HeightMm 为 0 → 自动读取打印机纸张配置

② HTML → PDF（PuppeteerSharp / Chromium）
   └─ 精确物理尺寸（毫米）、零边距、保留背景色

③ PDF → Bitmap（PDFtoImage + SkiaSharp）
   └─ 按打印机原生 DPI 光栅化

④ Bitmap → 打印机（System.Drawing.Printing）
   └─ 自适应 DPI 缩放，居中输出

⑤ 返回响应消息给客户端
```

---

## 客户端 SDK

### 安装

```bash
npm install @easyprint/js
```

### 快速开始

```ts
import { EasyPrintClient } from '@easyprint/js';

const client = new EasyPrintClient({
  host: '127.0.0.1',
  port: 8765,
});

// 监听连接状态
client.on('connected',    ()     => console.log('已连接'));
client.on('disconnected', (code) => console.log('断线', code));
client.on('reconnecting', (delay, attempt) =>
  console.log(`第 ${attempt} 次重连，等待 ${delay}ms`));

// 统一处理所有服务端响应
client.on('response', resp => {
  switch (resp.command) {
    case 'PRINT':
      console.log('打印成功，任务 ID:', resp.data);
      break;
    case 'LIST':
      console.log('打印机列表:', resp.data);
      break;
    case 'JOBS':
      console.log('队列任务:', resp.data);
      break;
    case 'CANCEL':
    case 'RESTART':
    case 'PAUSE':
    case 'RESUME':
      console.log(`${resp.command} 结果:`, resp.data);
      break;
  }
});

// 发送打印指令（断线时自动入队，重连后自动重发）
client.print({
  context:     '<html><body style="margin:0"><p>W0100025</p></body></html>',
  widthMm:     76,
  heightMm:    130,
  printerName: 'TSC TE200',   // 留空则使用服务端默认打印机
});
```

### API

#### `new EasyPrintClient(options?)`

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `host` | `string` | `'127.0.0.1'` | 服务端 IP |
| `port` | `number` | `8765` | 服务端端口 |
| `autoReconnect` | `boolean` | `true` | 断线后是否自动重连 |
| `reconnectDelay` | `number` | `1000` | 初始重连等待时间（ms） |
| `maxReconnectDelay` | `number` | `30000` | 最大重连等待时间（ms） |

#### 方法

| 方法 | 说明 |
|------|------|
| `print(job)` | 发送打印任务，断线时自动入队 |
| `list()` | 枚举本机所有已安装打印机 |
| `jobs(printerName?)` | 查询指定打印机的打印队列任务列表 |
| `cancel(jobIds, printerName?)` | 取消（删除）一个或多个队列任务 |
| `restart(jobIds, printerName?)` | 重启一个或多个队列任务（从头重打） |
| `pause(jobIds, printerName?)` | 暂停一个或多个队列任务 |
| `resume(jobIds, printerName?)` | 继续已暂停的一个或多个队列任务 |
| `send(message)` | 发送原始命令帧（高级用法） |
| `disconnect()` | 主动断开（autoReconnect=true 时仍会重连） |
| `reconnectNow()` | 立即重连，跳过等待，重置退避延迟 |
| `destroy()` | 彻底销毁客户端，清理所有定时器 |

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `state` | `ConnectionState` | 当前连接状态快照 |
| `isConnected` | `boolean` | 是否已连接 |
| `pendingCount` | `number` | 积压队列中的消息数量 |
| `url` | `string` | 服务端 WebSocket 地址 |

#### 事件

| 事件 | 参数 | 说明 |
|------|------|------|
| `connecting` | — | 开始建立连接 |
| `connected` | — | 连接成功，积压队列已冲刷 |
| `disconnected` | `code, reason` | 连接断开 |
| `error` | `Event` | WebSocket 底层错误 |
| `response` | `PrintResponse` | 收到服务端响应（所有命令共用此事件） |
| `stateChange` | `ConnectionState` | 连接状态变化 |
| `queued` | `message, length` | 消息因断线被入队 |
| `reconnecting` | `delay, attempt` | 正在等待重连 |

---

### `PrintJobRequest` 结构

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `context` | `string` | — | **必填**，要打印的 HTML 内容 |
| `printerName` | `string?` | `''` | 打印机名称，空 = 服务端默认打印机 |
| `widthMm` | `number?` | `76` | 标签宽度（毫米），为 `0` 时自动读取打印机配置，推荐显式指定以获得更快速度 |
| `heightMm` | `number?` | `130` | 标签高度（毫米），为 `0` 时自动读取打印机配置，推荐显式指定以获得更快速度 |
| `paddingMm` | `number[]?` | `[0,0,0,0]` | 页面填充（边距），单位毫米，顺序为上、右、下、左 |

### `PrintResponse` 结构

| 字段 | 类型 | 说明 |
|------|------|------|
| `command` | `string` | 触发响应的命令名，如 `"PRINT"` / `"JOBS"` |
| `status` | `number` | `200`=成功 / `207`=部分成功 / `400`=请求错误 / `500`=服务器错误 |
| `message` | `string` | 可读的结果描述 |
| `data` | `any` | 响应数据，类型因命令而异（见下表） |

### `data` 字段类型对应关系

| 命令 | `data` 类型 | 说明 |
|------|------------|------|
| `PRINT` | `string` | 服务端生成的任务 ID（8 位大写十六进制） |
| `LIST` | `PrinterInfo[]` | 已安装打印机列表 |
| `JOBS` | `PrintQueueJob[]` | 当前打印队列任务列表 |
| `CANCEL` / `RESTART` / `PAUSE` / `RESUME` | `JobControlResult[]` | 每个任务的操作结果 |

### `PrinterInfo` 结构

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | `string` | 打印机名称 |
| `isDefault` | `boolean` | 是否为系统默认打印机 |

### `PrintQueueJob` 结构

| 字段 | 类型 | 说明 |
|------|------|------|
| `jobId` | `number` | Windows 系统分配的任务 ID |
| `printerName` | `string` | 打印机名称 |
| `document` | `string` | 文档名称 |
| `userName` | `string` | 提交任务的用户名 |
| `statusLabel` | `string` | 人类可读状态，如 `"打印中"` / `"已完成"` / `"失败"` |
| `status` | `number` | 状态位标志（JOB_STATUS_* bitmask） |
| `totalPages` | `number` | 文档总页数（0 表示未知） |
| `pagesPrinted` | `number` | 已打印页数 |
| `priority` | `number` | 打印优先级（1–99） |
| `position` | `number` | 在队列中的位置（从 1 开始） |

### `JobControlResult` 结构

批量控制命令（CANCEL / RESTART / PAUSE / RESUME）响应 `data` 数组的单个元素：

| 字段 | 类型 | 说明 |
|------|------|------|
| `jobId` | `number` | 被操作的任务 ID |
| `ok` | `boolean` | 该任务操作是否成功 |

---

## 消息协议

服务端使用 `Split(' ', 2)` 只在**第一个空格**处切割，因此 HTML 内容中的空格不受影响。

**PRINT — 发送打印任务：**

```
客户端 → 服务端：
  PRINT {"printerName":"TSC TE200","context":"<html>...</html>","widthMm":76,"heightMm":130}

服务端 → 客户端：
  {"command":"PRINT","status":200,"message":"打印成功","data":"A3F1B2C0"}
```

**LIST — 获取打印机列表：**

```
客户端 → 服务端：
  LIST

服务端 → 客户端：
  {"command":"LIST","status":200,"message":"共 2 台打印机","data":[{"name":"TSC TE200","isDefault":true}]}
```

**JOBS — 查询打印队列：**

```
客户端 → 服务端：
  JOBS {"printerName":"TSC TE200"}   // printerName 可省略，省略时使用默认打印机
  JOBS

服务端 → 客户端：
  {"command":"JOBS","status":200,"message":"共 3 个任务","data":[
    {"jobId":1,"printerName":"TSC TE200","document":"标签","userName":"Admin",
     "statusLabel":"打印中","status":16,"totalPages":1,"pagesPrinted":0,"priority":1,"position":1}
  ]}
```

**CANCEL / RESTART / PAUSE / RESUME — 批量任务控制：**

```
客户端 → 服务端（以 CANCEL 为例，支持多个 jobId）：
  CANCEL {"printerName":"TSC TE200","jobIds":[5,6,7]}

服务端 → 客户端：
  {"command":"CANCEL","status":200,"message":"共 3 个任务：3 成功，0 失败",
   "data":[{"jobId":5,"ok":true},{"jobId":6,"ok":true},{"jobId":7,"ok":false}]}
```

> `status` 语义：`200` 全部成功 / `207` 部分成功 / `500` 全部失败

---

## 在 Angular 中使用

**environment.ts：**

```ts
export const environment = {
  easyPrint: {
    host: '127.0.0.1',
    port: 8765,
  },
};
```

**print.service.ts：**

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

  /** 打印标签 */
  printLabel(html: string, widthMm = 76, heightMm = 130): void {
    this.client.print({ context: html, widthMm, heightMm });
  }

  /** 查询打印队列 */
  getQueue(printerName?: string): void {
    this.client.jobs(printerName);
  }

  /** 取消任务 */
  cancelJobs(jobIds: number[], printerName?: string): void {
    this.client.cancel(jobIds, printerName);
  }

  /** 暂停任务 */
  pauseJobs(jobIds: number[], printerName?: string): void {
    this.client.pause(jobIds, printerName);
  }

  /** 继续任务 */
  resumeJobs(jobIds: number[], printerName?: string): void {
    this.client.resume(jobIds, printerName);
  }

  ngOnDestroy(): void {
    this.client.destroy();
  }
}
```

---

## 重连策略

采用指数退避，连接成功后自动重置：

```
断线 → 等 1s → 重连失败 → 等 2s → 重连失败 → 等 4s → … → 等 30s（封顶）
                                                              ↓
                                                         重连成功 → 重置为 1s
```

---

## 构建

```bash
npm install
npm run build   # 输出到 dist/
```

## 发布 npm

```bash
npm login
npm publish --access public
```
