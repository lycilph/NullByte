import WebSocket from 'ws';

export class NullByteError extends Error {
  constructor(public readonly code: string, message: string) { super(message); }
}

type Pending = { resolve: (value: any) => void; reject: (error: Error) => void };

export class NullByteClient {
  private socket?: WebSocket;
  private connecting?: Promise<WebSocket>;
  private readonly pending = new Map<number, Pending>();
  private nextId = 1;

  constructor(private readonly url: string) {}

  async request<T>(op: string, path: string, extra: object = {}): Promise<T> {
    const socket = await this.connect();
    const id = this.nextId++;
    return new Promise<T>((resolve, reject) => {
        this.pending.set(id, { resolve, reject });
        socket.send(JSON.stringify({ id, op, path, ...extra }));
    });
  }

  dispose() { this.socket?.close(); }

  private connect(): Promise<WebSocket> {
    if (this.socket?.readyState === WebSocket.OPEN) return Promise.resolve(this.socket);

    this.connecting ??= new Promise<WebSocket>((resolve, reject) => {
      const socket = new WebSocket(this.url);
      socket.on('open', () => { this.socket = socket; this.connecting = undefined; resolve(socket); });
      socket.on('message', data => this.onMessage(data.toString()));
      socket.on('error', error => { this.connecting = undefined; reject(error); });
      socket.on('close', () => {
        if (this.socket === socket) this.socket = undefined;
        this.failAll(new Error('Connection to NullByte closed'));
      });
    });
    return this.connecting;
  }

  private onMessage(text: string) {
    const message = JSON.parse(text);
    const pending = this.pending.get(message.id);   // messages without an id (events) come in Step 5
    if (!pending) return;
    this.pending.delete(message.id);

    if (message.ok) pending.resolve(message.result);
    else pending.reject(new NullByteError(message.error.code, message.error.message));
  }

  private failAll(error: Error) {
    for (const pending of this.pending.values()) pending.reject(error);
    this.pending.clear();
  }
}