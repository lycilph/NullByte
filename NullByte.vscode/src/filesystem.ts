import * as vscode from 'vscode';
import { NullByteClient, NullByteError } from './client';

const toFileType = (type: string) =>
  type === 'directory' ? vscode.FileType.Directory : vscode.FileType.File;

function toFileSystemError(error: unknown, uri: vscode.Uri): Error {
  if (error instanceof NullByteError) {
    switch (error.code) {
      case 'NotFound': return vscode.FileSystemError.FileNotFound(uri);
      case 'IsADirectory': return vscode.FileSystemError.FileIsADirectory(uri);
      case 'NotADirectory': return vscode.FileSystemError.FileNotADirectory(uri);
    }
  }
  return vscode.FileSystemError.Unavailable(error instanceof Error ? error.message : String(error));
}

export class NullByteFileSystem implements vscode.FileSystemProvider {
  private readonly emitter = new vscode.EventEmitter<vscode.FileChangeEvent[]>();
  readonly onDidChangeFile = this.emitter.event;   // fired from Step 5

  constructor(private readonly client: NullByteClient) {}

  watch(): vscode.Disposable { return new vscode.Disposable(() => {}); }

  async stat(uri: vscode.Uri): Promise<vscode.FileStat> {
    const r = await this.call<{ type: string; size: number }>('stat', uri);
    return { type: toFileType(r.type), ctime: 0, mtime: 0, size: r.size };
  }

  async readDirectory(uri: vscode.Uri): Promise<[string, vscode.FileType][]> {
    const r = await this.call<{ entries: { name: string; type: string }[] }>('readDirectory', uri);
    return r.entries.map(e => [e.name, toFileType(e.type)]);
  }

  async readFile(uri: vscode.Uri): Promise<Uint8Array> {
    const r = await this.call<{ content: string }>('readFile', uri);
    return Buffer.from(r.content, 'utf8');
  }

  writeFile(): never { throw vscode.FileSystemError.NoPermissions('Read-only for now'); }
  createDirectory(): never { throw vscode.FileSystemError.NoPermissions('Not supported'); }
  delete(): never { throw vscode.FileSystemError.NoPermissions('Read-only for now'); }
  rename(): never { throw vscode.FileSystemError.NoPermissions('Read-only for now'); }

  private async call<T>(op: string, uri: vscode.Uri): Promise<T> {
    try { return await this.client.request<T>(op, uri.path); }
    catch (error) { throw toFileSystemError(error, uri); }
  }
}