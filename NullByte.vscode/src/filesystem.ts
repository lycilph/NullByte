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
      case 'FileExists': return vscode.FileSystemError.FileExists(uri);
      case 'NoPermissions': return vscode.FileSystemError.NoPermissions(error.message);
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

  async writeFile(uri: vscode.Uri, content: Uint8Array,
                  options: { create: boolean; overwrite: boolean }): Promise<void> {
    const r = await this.call<{ created: boolean }>('writeFile', uri, {
      content: new TextDecoder().decode(content), create: options.create, overwrite: options.overwrite });
    this.emitter.fire([{ type: r.created ? vscode.FileChangeType.Created : vscode.FileChangeType.Changed, uri }]);
  }

  async delete(uri: vscode.Uri): Promise<void> {
    await this.call('delete', uri);
    this.emitter.fire([{ type: vscode.FileChangeType.Deleted, uri }]);
  }

  async rename(oldUri: vscode.Uri, newUri: vscode.Uri, options: { overwrite: boolean }): Promise<void> {
    await this.call('rename', oldUri, { newPath: newUri.path, overwrite: options.overwrite });
    this.emitter.fire([
      { type: vscode.FileChangeType.Deleted, uri: oldUri },
      { type: vscode.FileChangeType.Created, uri: newUri }]);
  }
  
  createDirectory(): never { throw vscode.FileSystemError.NoPermissions('Not supported'); }

  private async call<T>(op: string, uri: vscode.Uri, extra: object = {}): Promise<T> {
    try { return await this.client.request<T>(op, uri.path, extra); }
    catch (error) { throw toFileSystemError(error, uri); }
  }
}