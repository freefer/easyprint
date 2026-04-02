# @easyprint/js

EasyPrint 本地打印服务的 WebSocket 客户端 SDK，框架无关，支持所有主流浏览器环境。

## 安装

```bash
npm install @easyprint/js
```

## 快速开始

```ts
import { EasyPrintClient } from '@easyprint/js';

const client = new EasyPrintClient({
  host: '127.0.0.1',
  port: 8765,
});

// 监听连接状态
client.on('connected',    ()     => console.log('✅ 已连接'));
client.on('disconnected', (code) => console.log('⚠️ 断线', code));
client.on('reconnecting', (delay, attempt) =>
  console.log(`🔄 第 ${attempt} 次重连，等待 ${delay}ms`));

// 监听打印结果
client.on('response', resp => {
  if (resp.status === 200) {
    console.log('打印成功，任务 ID:', resp.data);
  } else {
    console.error('打印失败:', resp.message);
  }
});

// 发送打印指令
client.print({
  context:  '<html><body style="margin:0"><p>W0100025</p></body></html>',
  widthMm:  76,
  heightMm: 130,
  printerName: 'TSC TE200',   // 留空则使用服务端默认打印机
});
```

## API

### `new EasyPrintClient(options?)`

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `host` | `string` | `'127.0.0.1'` | 服务端 IP |
| `port` | `number` | `8765` | 服务端端口 |
| `autoReconnect` | `boolean` | `true` | 断线后是否自动重连 |
| `reconnectDelay` | `number` | `1000` | 初始重连等待时间（ms） |
| `maxReconnectDelay` | `number` | `30000` | 最大重连等待时间（ms） |

### 方法

| 方法 | 说明 |
|------|------|
| `print(job)` | 发送打印任务，断线时自动入队 |
| `disconnect()` | 主动断开（autoReconnect=true 时仍会重连） |
| `reconnectNow()` | 立即重连，跳过等待，重置退避延迟 |
| `destroy()` | 彻底销毁客户端，清理所有定时器 |

### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `state` | `ConnectionState` | 当前连接状态快照 |
| `isConnected` | `boolean` | 是否已连接 |
| `pendingCount` | `number` | 积压队列中的消息数量 |
| `url` | `string` | 服务端 WebSocket 地址 |

### 事件

| 事件 | 参数 | 说明 |
|------|------|------|
| `connecting` | — | 开始建立连接 |
| `connected` | — | 连接成功，积压队列已冲刷 |
| `disconnected` | `code, reason` | 连接断开 |
| `error` | `Event` | WebSocket 底层错误 |
| `response` | `PrintResponse` | 收到服务端响应 |
| `stateChange` | `ConnectionState` | 连接状态变化 |
| `queued` | `message, length` | 消息因断线被入队 |
| `reconnecting` | `delay, attempt` | 正在等待重连 |

### `PrintJobRequest` 结构

对应服务端 C# `PrintJob.cs`：

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `context` | `string` | — | **必填**，要打印的 HTML 内容 |
| `printerName` | `string?` | `''` | 打印机名称，空=服务端默认打印机 |
| `widthMm` | `number?` | `76` | 标签宽度（毫米） |
| `heightMm` | `number?` | `130` | 标签高度（毫米） |

### `PrintResponse` 结构

| 字段 | 类型 | 说明 |
|------|------|------|
| `status` | `number` | 200=成功 / 400=请求错误 / 500=服务器错误 |
| `message` | `string` | 可读的结果描述 |
| `data` | `string?` | 服务端生成的任务 ID |

## 消息协议

服务端使用 `Split(' ', 2)` 只在**第一个空格**处切割，因此 HTML 内容中的空格不受影响：

```
PRINT {"printerName":"TSC","context":"<html> hello world </html>","widthMm":76,"heightMm":130}
──┬──  ──────────────────────────────────────────────────────────────────────────────────────
  │    Parameters[0] = 完整 JSON（HTML 内嵌空格安全）
  Key = "PRINT"
```

## 在 Angular 中使用

```ts
import { EasyPrintClient } from '@easyprint/js';
import { environment } from 'src/environments/environment';

@Injectable({ providedIn: 'root' })
export class PrintService {
  private readonly client = new EasyPrintClient(environment.easyPrint);

  print(html: string, widthMm = 76, heightMm = 130) {
    this.client.print({ context: html, widthMm, heightMm });
  }

  readonly response$ = new Observable<PrintResponse>(observer => {
    const handler = (resp: PrintResponse) => observer.next(resp);
    this.client.on('response', handler);
    return () => this.client.off('response', handler);
  });
}
```

## 重连策略

采用指数退避，连接成功后自动重置：

```
断线 → 等 1s → 重连失败 → 等 2s → 重连失败 → 等 4s → … → 等 30s（封顶）
                                                              ↓
                                                         重连成功 → 重置为 1s
```

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
