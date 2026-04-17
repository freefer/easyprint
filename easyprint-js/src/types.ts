// ── 连接状态 ──────────────────────────────────────────────────────────────────

export type ConnectionState =
  | 'connecting'
  | 'connected'
  | 'disconnected'
  | 'error';

// ── 配置项 ────────────────────────────────────────────────────────────────────

export interface EasyPrintOptions {
  /** 服务端 IP 地址，默认 127.0.0.1 */
  host?: string;
  /** 服务端端口，默认 8765 */
  port?: number;
  /**
   * 是否在断线后自动重连，默认 true。
   * 重连采用指数退避策略：1s → 2s → 4s → … → maxReconnectDelay
   */
  autoReconnect?: boolean;
  /** 初始重连等待时间（ms），默认 1000 */
  reconnectDelay?: number;
  /** 最大重连等待时间（ms），默认 30000 */
  maxReconnectDelay?: number;
}

// ── 打印任务（对应 C# PrintJob）──────────────────────────────────────────────

/**
 * 打印任务请求，字段与 C# `PrintJob.cs` 对应：
 *
 * | TypeScript     | C# PrintJob   | 说明                |
 * |----------------|---------------|---------------------|
 * | printerName    | PrinterName   | 打印机名称          |
 * | context        | Context       | 要打印的 HTML 内容  |
 * | widthMm        | WidthMm       | 标签宽度（mm）      |
 * | heightMm       | HeightMm      | 标签高度（mm）      |
 *
 * `Id`、`CreateTime`、`Status` 由服务端自动生成/管理。
 */
export interface PrintJobRequest {
  /** 打印机名称，留空使用服务端配置的默认打印机 */
  printerName?: string;
  /** 要打印的 HTML 内容 */
  context: string;
  /** 标签宽度（毫米），默认 76 */
  widthMm?: number;
  /** 标签高度（毫米），默认 130 */
  heightMm?: number;
  /** 页面填充（或边距），单位：毫米，默认 0 0 0 0 表示上右下左 */
  paddingMm?: number[];
}

// ── 服务端响应（对应 C# PrintResponseMessage）────────────────────────────────

export interface PrintResponse {
  /** 触发本次响应的命令，如 "PRINT" / "LIST" */
  command?: string;
  /** 200 = 成功 / 400 = 请求参数错误 / 500 = 服务器错误 */
  status: number;
  /** 可读的结果描述 */
  message: string;
  /** 服务端生成的任务 ID 或附加数据 */
  data?: any;
}

// ── JOBS 命令 / 打印队列条目（对应 C# PrintQueueJob）────────────────────────

/**
 * Windows 打印队列中的单个任务信息，对应服务端 `PrintQueueJob.cs`。
 *
 * 通过 `client.jobs()` 获取，响应示例：
 * ```ts
 * client.on('response', resp => {
 *   if (resp.command === 'JOBS' && resp.status === 200) {
 *     const jobs = resp.data as PrintQueueJob[];
 *     console.log(jobs);
 *   }
 * });
 * ```
 */
export interface PrintQueueJob {
  /** Windows 系统分配的任务 ID */
  jobId: number;
  /** 打印机名称 */
  printerName: string;
  /** 文档名称 */
  document: string;
  /** 提交任务的用户名 */
  userName: string;
  /** 驱动返回的原始状态文本（可为空） */
  statusText: string;
  /**
   * 状态位标志（JOB_STATUS_* bitmask）。
   * 常用位：0x10=打印中 / 0x08=后台处理 / 0x02=错误 / 0x80=已打印 / 0x1000=已完成
   */
  status: number;
  /** 人类可读的状态描述，如 "打印中" / "已完成" / "失败" */
  statusLabel: string;
  /** 文档总页数，0 表示未知 */
  totalPages: number;
  /** 已打印页数 */
  pagesPrinted: number;
  /** 打印优先级（1–99） */
  priority: number;
  /** 在队列中的位置（从 1 开始） */
  position: number;
}

// ── 批量任务控制命令响应（CANCEL / RESTART / PAUSE / RESUME）─────────────────

/**
 * 批量任务控制命令（CANCEL / RESTART / PAUSE / RESUME）响应的单个结果条目。
 *
 * 每个 `jobId` 对应一个 `ok` 表示该任务操作是否成功。
 */
export interface JobControlResult {
  /** 被操作的任务 ID */
  jobId: number;
  /** 该任务操作是否成功 */
  ok: boolean;
}

// ── LIST 命令相关类型 ─────────────────────────────────────────────────────────

/**
 * 打印机信息条目，对应 C# `LIST` 命令 `data` 字段反序列化后的单个元素。
 *
 * 使用示例：
 * ```ts
 * client.on('response', resp => {
 *   if (resp.command === 'LIST' && resp.status === 200) {
 *     const printers: PrinterInfo[] = JSON.parse(resp.data ?? '[]');
 *   }
 * });
 * client.list();
 * ```
 */
export interface PrinterInfo {
  /** 打印机名称 */
  name: string;
  /** 是否为系统默认打印机 */
  isDefault: boolean;
}

// ── 事件映射（强类型事件系统）──────────────────────────────────────────────

export interface EasyPrintEventMap extends Record<string, unknown[]> {
  /** 正在建立连接 */
  connecting: [];
  /** 连接建立成功，积压队列已冲刷 */
  connected: [];
  /** 连接断开 */
  disconnected: [code: number, reason: string];
  /** WebSocket 底层错误（通常紧跟 disconnected） */
  error: [event: Event];
  /** 收到服务端响应 */
  response: [resp: PrintResponse];
  /** 状态发生变化 */
  stateChange: [state: ConnectionState];
  /** 消息因断线被加入等待队列 */
  queued: [message: string, queueLength: number];
  /** 重连等待开始，delay 为本次等待时间（ms） */
  reconnecting: [delay: number, attempt: number];
}
