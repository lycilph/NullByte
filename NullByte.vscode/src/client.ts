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
type Pending = { op: string; started: number; resolve: (value: any) => void; reject: (error: Error) => void };

export type Connection = { url: string; token: string; pid?: number  };
export type ConnectionState = { status: 'connected' | 'connecting' | 'disconnected'; detail: string };

const ReconnectDelayMs = 2000;

/** Whether a process with this id exists on this machine. Only used for log hints. */
function isAlive(pid: number): boolean {
  try { process.kill(pid, 0); return true; }
  catch (error) { return (error as NodeJS.ErrnoException).code === 'EPERM'; }
}

export class NullByteClient {
  private socket?: WebSocket;
  private connecting?: Promise<WebSocket>;
  private reconnectTimer?: NodeJS.Timeout;
  private immediateReconnect = false;
  private disposed = false;
  private readonly pending = new Map<number, Pending>();
  private nextId = 1;
  private eventHandler?: (event: ServerEvent) => void;
  private connectedHandler?: () => void;
  private currentState: ConnectionState = { status: 'connecting', detail: 'Connecting to the game…' };
  private stateHandler?: (state: ConnectionState) => void;

  constructor(
    private readonly log: Logger, 
    private readonly readConnection: () => Connection) {}

  onEvent(handler: (event: ServerEvent) => void) { this.eventHandler = handler; }
  onConnected(handler: () => void) { this.connectedHandler = handler; }

  get state(): ConnectionState { return this.currentState; }
  onStateChange(handler: (state: ConnectionState) => void) { this.stateHandler = handler; }

  private setState(status: ConnectionState['status'], detail: string) {
    if (this.currentState.status === status && this.currentState.detail === detail) return;   // retries must not flicker
    this.currentState = { status, detail };
    this.stateHandler?.(this.currentState);
  }

  /** Connect now, and keep reconnecting in the background so push events keep flowing. */
  start() { this.connect().catch(() => {}); }

  async request<T>(op: string, path: string, extra: object = {}): Promise<T> {
    const socket = await this.connect();
    const id = this.nextId++;
    this.log.debug(`→ #${id} ${op} ${path}`);
    return new Promise<T>((resolve, reject) => {
      this.pending.set(id, { op, started: Date.now(), resolve, reject });
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

    this.connecting ??= this.open()
      .catch(error => { this.scheduleReconnect(); throw error; })
      .finally(() => { this.connecting = undefined; });
    return this.connecting;
  }

  private async open(): Promise<WebSocket> {
    let connection: Connection;
    try {
      connection = this.readConnection();
    } catch (error) {
      const failure = error as NodeJS.ErrnoException;
      const missing = failure.code === 'ENOENT';

      this.setState('disconnected', missing
        ? `No connection file${failure.path ? ` at ${failure.path}` : ''}. Start the game with --vscode.`
        : `Cannot read the connection file: ${failure.message}`);

      // A missing file is the normal "game not running with --vscode" state, so it stays quiet.
      this.log[missing ? 'debug' : 'warn'](missing
        ? 'No connection file (is the game running with --vscode?)'
        : `Cannot read the connection file: ${error instanceof Error ? error.message : error}`);
      throw error;
    }

    this.log.debug(`Connecting to ${connection.url}`);
    return new Promise<WebSocket>((resolve, reject) => {
      const socket = new WebSocket(connection.url, { headers: { Authorization: `Bearer ${connection.token}` } });

      socket.on('open', () => {
        this.socket = socket;
        this.log.info('Connected');
        this.setState('connected', `Connected to ${connection.url}`);
        resolve(socket);
        this.connectedHandler?.();
      });
      socket.on('message', data => this.onMessage(data.toString()));
      socket.on('error', error => {
        const rejected = / 40[13]$/.test(error.message);   // "Unexpected server response: 401"
        const stale = connection.pid !== undefined && !isAlive(connection.pid);

        this.setState('disconnected',
            rejected ? 'The game rejected the token. It was probably restarted. Reconnecting…'
            : stale ? `The game (pid ${connection.pid}) is not running, so the connection file is stale. Start the game with --vscode.`
            : `Cannot reach the game at ${connection.url}: ${error.message}`);

        this.log[rejected ? 'warn' : 'debug'](
          `Socket error: ${error.message}` +
          (stale ? ` (the game, pid ${connection.pid}, is not running: stale connection file?)` : ''));
        reject(error);
      });
      socket.on('close', () => {
        if (this.socket === socket) {
          this.socket = undefined;
          this.log.info('Disconnected');
          if (!this.immediateReconnect)   // an explicit reconnect() already says "Reconnecting…"
            this.setState('disconnected', 'The connection to the game was closed. Reconnecting…');
        }
        this.failAll(new Error('Connection to NullByte closed'));
        this.scheduleReconnect();
      });
    });
  }

  reconnect() {
    this.setState('connecting', 'Reconnecting…');

    clearTimeout(this.reconnectTimer);
    this.reconnectTimer = undefined;

    if (this.socket) {
      this.immediateReconnect = true;   // the close handler reconnects without the usual delay
      this.socket.close();
    } else {
      this.connect().catch(() => {});
    }
  }

  private scheduleReconnect() {
    if (this.disposed || this.reconnectTimer) return;

    const delay = this.immediateReconnect ? 0 : ReconnectDelayMs;
    this.immediateReconnect = false;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = undefined;
      this.connect().catch(() => {});
    }, delay);
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

    const ms = Date.now() - pending.started;
    if (ms > 1000) this.log.warn(`Slow response: ${pending.op} took ${ms} ms`);

    if (message.ok) {
      this.log.debug(`← #${message.id} ${pending.op} ok (${ms} ms)`);
      pending.resolve(message.result);
    } else {
      this.log.debug(`← #${message.id} ${pending.op} ${message.error.code}: ${message.error.message} (${ms} ms)`);
      pending.reject(new NullByteError(message.error.code, message.error.message));
    }
  }

  private failAll(error: Error) {
    for (const pending of this.pending.values()) pending.reject(error);
    this.pending.clear();
  }
}