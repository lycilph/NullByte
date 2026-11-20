import * as vscode from 'vscode';
import { Logger, NullByteClient, NullByteError, ServerEvent } from './client';
import { RootName, rootUri, toGamePath, toUri } from './paths';

const changeTypes: Record<string, vscode.FileChangeType> = {
  created: vscode.FileChangeType.Created,
  changed: vscode.FileChangeType.Changed,
  deleted: vscode.FileChangeType.Deleted,
};
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

  constructor(private readonly client: NullByteClient, private readonly log: Logger) {}

  watch(): vscode.Disposable { return new vscode.Disposable(() => {}); }

  async stat(uri: vscode.Uri): Promise<vscode.FileStat> {
    if (uri.path === '/') return { type: vscode.FileType.Directory, ctime: 0, mtime: 0, size: 0 };

    const r = await this.call<{ type: string; size: number; created: number; modified: number }>('stat', uri);
    return { type: toFileType(r.type), ctime: r.created, mtime: r.modified, size: r.size };
  }

  async readDirectory(uri: vscode.Uri): Promise<[string, vscode.FileType][]> {
    if (uri.path === '/') return [[RootName, vscode.FileType.Directory]];

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
  }

  async delete(uri: vscode.Uri): Promise<void> {
    await this.call('delete', uri);
  }

  async rename(oldUri: vscode.Uri, newUri: vscode.Uri, options: { overwrite: boolean }): Promise<void> {
    const newPath = toGamePath(newUri);
    if (newPath === undefined) throw vscode.FileSystemError.NoPermissions('Files cannot be moved out of the game');

    await this.call('rename', oldUri, { newPath, overwrite: options.overwrite });
  }
  
  createDirectory(): never { throw vscode.FileSystemError.NoPermissions('Not supported'); }

  private async call<T>(op: string, uri: vscode.Uri, extra: object = {}): Promise<T> {
    const path = toGamePath(uri);

    // Outside the game folder, or the dot-files VS Code probes for (.vscode/settings.json and the like).
    if (path === undefined || path.startsWith('/.')) throw vscode.FileSystemError.FileNotFound(uri);

    try { return await this.client.request<T>(op, path, extra); }
    catch (error) { throw toFileSystemError(error, uri); }
  }

  handleEvent(e: ServerEvent) {
    switch (e.event) {
      case 'fileChanged': {
        const type = changeTypes[e.type];
        if (type === undefined) { this.log.warn(`Unknown change type '${e.type}'`); return; }
        this.log.info(`Event: ${e.type} ${e.path}`);
        this.fire([{ type, uri: toUri(e.path) }]);
        break;
      }
      case 'reset':
        this.log.info('Event: the game was replaced, refreshing');
        void this.refreshAll();
        break;
      default:
        this.log.warn(`Unknown event '${e.event}'`);
    }
  }

  /** Fires a change for the root and every server folder, so the explorer re-reads them. */
  async refreshAll() {
    try {
      const servers = await this.readDirectory(rootUri);
      const open = vscode.workspace.textDocuments.filter(d => d.uri.scheme === 'nullbyte').map(d => d.uri);

      const uris = [rootUri, ...servers.map(([name]) => toUri(`/${name}`)), ...open];
      this.fire(uris.map(uri => ({ type: vscode.FileChangeType.Changed, uri })));

      // Change events do not make the explorer re-read a folder it failed to load earlier,
      // so refresh it exactly as the Refresh Explorer button would.
      await vscode.commands.executeCommand('workbench.files.action.refreshFilesExplorer');
    } catch (error) {
      this.log.warn(`Refresh failed: ${error instanceof Error ? error.message : error}`);
    }
  }

  private fire(changes: vscode.FileChangeEvent[]) {
    this.log.debug(`Firing ${changes.map(c => `${vscode.FileChangeType[c.type]} ${c.uri.path}`).join(', ')}`);
    this.emitter.fire(changes);
  }
}