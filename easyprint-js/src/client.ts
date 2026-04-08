import type {
  ConnectionState,
  EasyPrintEventMap,
  EasyPrintOptions,
  PrinterInfo,
  PrintJobRequest,
  PrintResponse,
} from './types';

// ── 轻量事件发射器（无 Node.js 依赖，纯浏览器可用）──────────────────────────

type Listener<Args extends unknown[]> = (...args: Args) => void;

class TypedEmitter<Events extends Record<string, unknown[]>> {
  private readonly _listeners = new Map<keyof Events, Set<Listener<any>>>();

  on<K extends keyof Events>(event: K, listener: Listener<Events[K]>): this {
    let set = this._listeners.get(event);
    if (!set) { set = new Set(); this._listeners.set(event, set); }
    set.add(listener);
    return this;
  }

  off<K extends keyof Events>(event: K, listener: Listener<Events[K]>): this {
    this._listeners.get(event)?.delete(listener);
    return this;
  }

  once<K extends keyof Events>(event: K, listener: Listener<Events[K]>): this {
    const wrapper = (...args: Events[K]) => {
      this.off(event, wrapper as any);
      (listener as any)(...args);
    };
    return this.on(event, wrapper as any);
  }

  protected emit<K extends keyof Events>(event: K, ...args: Events[K]): void {
    this._listeners.get(event)?.forEach(fn => {
      try { fn(...args); } catch { /* 防止单个监听器报错影响其他 */ }
    });
  }

  removeAllListeners(event?: keyof Events): void {
    if (event) this._listeners.delete(event);
    else       this._listeners.clear();
  }
}

// ── EasyPrintClient ──────────────────────────────────────────────────────────

const DEFAULTS: Required<EasyPrintOptions> = {
  host:              '127.0.0.1',
  port:              8765,
  autoReconnect:     true,
  reconnectDelay:    1_000,
  maxReconnectDelay: 30_000,
};

/**
 * EasyPrint WebSocket 客户端。
 *
 * ---
 *
 * **消息协议**（与服务端 `JsonPackageConverter.Map` 及 `JsonCommandBase<T>` 对应）
 *
 * 发送格式：`命令 <JSON>`
 * ```
 * PRINT {"printerName":"TSC TE200","context":"<html>...</html>","widthMm":76,"heightMm":130}
 * ```
 * 服务端用 `Split(' ', 2)` 在第一个空格处切割：
 * - `Key`           = "PRINT"
 * - `Parameters[0]` = 完整 JSON（HTML 内容中的空格不受影响）
 *
 * 响应格式：
 * ```json
 * { "status": 200, "message": "打印成功", "data": "A3F1B2C0" }
 * ```
 *
 * ---
 *
 * **重连策略**：指数退避，1s → 2s → 4s → … → maxReconnectDelay
 *
 * **断线队列**：`print()` 在断线时将消息放入队列，重连成功后按序重发。
 *
 * ---
 *
 * @example
 * ```ts
 * const client = new EasyPrintClient({ host: '192.168.1.100', port: 8765 });
 *
 * client.on('connected', () => console.log('Ready'));
 * client.on('response',  resp => console.log(resp));
 *
 * client.print({ context: '<html>Hello</html>', widthMm: 76, heightMm: 130 });
 * ```
 */
export class EasyPrintClient extends TypedEmitter<EasyPrintEventMap> {

  private readonly opts: Required<EasyPrintOptions>;

  private ws:               WebSocket | null = null;
  private reconnectTimer:   ReturnType<typeof setTimeout> | null = null;
  private reconnectAttempt  = 0;
  private currentDelay:     number;
  private _state:           ConnectionState = 'disconnected';
  private _destroyed        = false;

  /** 断线期间积压的消息，重连后按序重发 */
  private readonly pendingQueue: string[] = [];

  // ── 构造 ─────────────────────────────────────────────────────────────────

  constructor(options: EasyPrintOptions = {}) {
    super();
    this.opts         = { ...DEFAULTS, ...options };
    this.currentDelay = this.opts.reconnectDelay;
    this.connect();
  }

  // ── 只读属性 ─────────────────────────────────────────────────────────────

  /** 当前连接状态 */
  get state(): ConnectionState { return this._state; }

  /** 是否已连接 */
  get isConnected(): boolean { return this._state === 'connected'; }

  /** 等待发送的积压消息数量 */
  get pendingCount(): number { return this.pendingQueue.length; }

  /** WebSocket 服务端地址 */
  get url(): string {
    return `ws://${this.opts.host}:${this.opts.port}`;
  }

  // ── 连接管理 ─────────────────────────────────────────────────────────────

  /** 主动断开连接（若 autoReconnect=true，断开后仍会重连） */
  disconnect(): void {
    this.ws?.close(1000, 'Manual disconnect');
  }

  /** 立即重连（跳过当前等待，重置退避延迟） */
  reconnectNow(): void {
    this.currentDelay = this.opts.reconnectDelay;
    this.clearTimer();
    this.ws?.close();
    // ws.onclose 会触发新一轮 connect()
  }

  /** 永久销毁客户端，清除所有定时器与监听器 */
  destroy(): void {
    this._destroyed = true;
    this.clearTimer();
    this.ws?.close(1000, 'Destroyed');
    this.removeAllListeners();
  }

  // ── 打印 ─────────────────────────────────────────────────────────────────

  /**
   * 发送打印指令。
   *
   * 若当前未连接，消息会进入等待队列，重连成功后自动重发。
   *
   * @param job 打印任务，字段对应 C# `PrintJob`
   */
  print(job: PrintJobRequest): void {
    const payload: Record<string, unknown> = {
      printerName: job.printerName ?? '',
      context:     job.context,
      widthMm:     job.widthMm  ?? 76,
      heightMm:    job.heightMm ?? 130,
    };

    // 格式：命令 + 一个空格 + 紧凑 JSON
    // 服务端 Split(' ', 2) 只在第一个空格处切割，HTML 内容中的空格不受影响
    const message = `PRINT ${JSON.stringify(payload)}`;
    this.send(message);
  }

  // ── LIST ─────────────────────────────────────────────────────────────────

  /**
   * 请求服务端枚举本机所有已安装的打印机。
   *
   * 服务端返回 `command = "LIST"` 的响应，`data` 字段为 `PrinterInfo[]` 的 JSON 字符串。
   *
   * @example
   * ```ts
   * client.on('response', resp => {
   *   if (resp.command === 'LIST' && resp.status === 200) {
   *     const printers: PrinterInfo[] = JSON.parse(resp.data ?? '[]');
   *     console.log(printers);
   *   }
   * });
   * client.list();
   * ```
   */
  list(): void {
    // C# LIST 命令无需 JSON 体，直接发送命令字符串
    this.send('LIST');
  }

  // ── 底层发送 ─────────────────────────────────────────────────────────────

  /**
   * 向服务端发送原始字符串帧。
   *
   * 若当前未连接，消息会进入等待队列，重连成功后自动重发。
   * 通常应优先使用 `print()` / `list()` 等高层方法；
   * 仅在需要发送自定义命令时才直接调用此方法。
   *
   * @param message 遵循服务端协议的原始消息，格式为 `命令 <JSON>` 或纯命令字符串
   */
  send(message: string): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(message);
    } else {
      this.pendingQueue.push(message);
      this.emit('queued', message, this.pendingQueue.length);
    }
  }

  // ── 私有方法 ─────────────────────────────────────────────────────────────

  private setState(state: ConnectionState): void {
    if (this._state === state) return;
    this._state = state;
    this.emit('stateChange', state);
  }

  private connect(): void {
    if (this._destroyed) return;

    this.clearTimer();
    this.setState('connecting');
    this.emit('connecting');

    let socket: WebSocket;
    try {
      socket = new WebSocket(this.url);
    } catch (e) {
      // URL 格式错误等同步异常
      this.setState('error');
      this.scheduleReconnect();
      return;
    }

    // ── onopen ──────────────────────────────────────────────────────────
    socket.onopen = () => {
      this.ws             = socket;
      this.reconnectAttempt = 0;
      this.currentDelay   = this.opts.reconnectDelay;

      this.setState('connected');
      this.emit('connected');

      // 冲刷断线期间积压的消息（保持发送顺序）
      const count = this.pendingQueue.length;
      while (this.pendingQueue.length > 0) {
        socket.send(this.pendingQueue.shift()!);
      }
      if (count > 0) {
        // 可选：通知调用方队列已冲刷
      }
    };

    // ── onmessage ───────────────────────────────────────────────────────
    socket.onmessage = ({ data }) => {
      try {
        const resp = JSON.parse(data as string) as PrintResponse;
        this.emit('response', resp);
      } catch {
        // 忽略非合法 JSON 的原始帧（心跳包等）
      }
    };

    // ── onerror ─────────────────────────────────────────────────────────
    socket.onerror = (event) => {
      this.setState('error');
      this.emit('error', event);
      // onclose 会在 onerror 之后触发，由 onclose 负责重连调度
    };

    // ── onclose ─────────────────────────────────────────────────────────
    socket.onclose = ({ code, reason }) => {
      this.ws = null;

      if (this._destroyed || code === 1000) {
        // 正常关闭或已销毁，不重连
        this.setState('disconnected');
        this.emit('disconnected', code, reason);
        return;
      }

      this.setState('disconnected');
      this.emit('disconnected', code, reason);

      if (this.opts.autoReconnect) {
        this.scheduleReconnect();
      }
    };
  }

  /**
   * 指数退避调度重连。
   * 序列：1s → 2s → 4s → 8s → 16s → 30s（封顶） → 30s → …
   */
  private scheduleReconnect(): void {
    if (this._destroyed || !this.opts.autoReconnect) return;
    this.clearTimer();

    this.reconnectAttempt++;
    const delay = this.currentDelay;
    this.emit('reconnecting', delay, this.reconnectAttempt);

    this.reconnectTimer = setTimeout(() => {
      this.currentDelay = Math.min(
        this.currentDelay * 2,
        this.opts.maxReconnectDelay,
      );
      this.connect();
    }, delay);
  }

  private clearTimer(): void {
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }
}
