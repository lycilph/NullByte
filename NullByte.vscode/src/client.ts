import WebSocket from 'ws';

export interface Logger {
  trace(message: string): void;
  debug(message: string): void;
  info(message: string): void;
  warn(message: string): void;
}

export class NullByteError extends Error {
  constructor(public readonly code: string, message: string) { super(message); }
}

export type ServerEvent = { event: string; [key: string]: any };
type Pending = { resolve: (value: any) => void; reject: (error: Error) => void };

const ReconnectDelayMs = 2000;

export class NullByteClient {
  private socket?: WebSocket;
  private connecting?: Promise<WebSocket>;
  private reconnectTimer?: NodeJS.Timeout;
  private disposed = false;
  private readonly pending = new Map<number, Pending>();
  private nextId = 1;
  private eventHandler?: (event: ServerEvent) => void;
  private connectedHandler?: () => void;

  constructor(private readonly url: string, private readonly log: Logger) {}

  onEvent(handler: (event: ServerEvent) => void) { this.eventHandler = handler; }
  onConnected(handler: () => void) { this.connectedHandler = handler; }

  /** Connect now, and keep reconnecting in the background so push events keep flowing. */
  start() { this.connect().catch(() => {}); }

  async request<T>(op: string, path: string, extra: object = {}): Promise<T> {
    const socket = await this.connect();
    const id = this.nextId++;
    this.log.debug(`→ #${id} ${op} ${path}`);
    return new Promise<T>((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, op, path, ...extra }));
    });
  }

  dispose() {
    this.disposed = true;
    clearTimeout(this.reconnectTimer);
    this.socket?.close();
  }

  private connect(): Promise<WebSocket> {
    if (this.socket?.readyState === WebSocket.OPEN) return Promise.resolve(this.socket);

    this.connecting ??= new Promise<WebSocket>((resolve, reject) => {
      this.log.debug(`Connecting to ${this.url}`);
      const socket = new WebSocket(this.url);

      socket.on('open', () => {
        this.socket = socket;
        this.connecting = undefined;
        this.log.info('Connected');
        resolve(socket);
        this.connectedHandler?.();
      });
      socket.on('message', data => this.onMessage(data.toString()));
      socket.on('error', error => {
        this.connecting = undefined;
        this.log.debug(`Socket error: ${error.message}`);
        reject(error);
      });
      socket.on('close', () => {
        if (this.socket === socket) {
          this.socket = undefined;
          this.log.info('Disconnected');
        }
        this.failAll(new Error('Connection to NullByte closed'));
        this.scheduleReconnect();
      });
    });
    return this.connecting;
  }

  private scheduleReconnect() {
    if (this.disposed || this.reconnectTimer) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = undefined;
      this.connect().catch(() => {});
    }, ReconnectDelayMs);
  }

  private onMessage(text: string) {
    const message = JSON.parse(text);

    if (message.event) {
      this.log.trace(`← ${text}`);
      this.eventHandler?.(message);
      return;
    }

    const pending = this.pending.get(message.id);
    if (!pending) { this.log.warn(`Response for unknown request #${message.id}`); return; }
    this.pending.delete(message.id);

    if (message.ok) {
      this.log.debug(`← #${message.id} ok`);
      pending.resolve(message.result);
    } else {
      this.log.debug(`← #${message.id} ${message.error.code}: ${message.error.message}`);
      pending.reject(new NullByteError(message.error.code, message.error.message));
    }
  }

  private failAll(error: Error) {
    for (const pending of this.pending.values()) pending.reject(error);
    this.pending.clear();
  }
}