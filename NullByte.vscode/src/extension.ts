import * as vscode from 'vscode';
import { NullByteClient } from './client';
import { NullByteFileSystem } from './filesystem';

export function activate(context: vscode.ExtensionContext) {
  const log = vscode.window.createOutputChannel('NullByte', { log: true });
  const client = new NullByteClient('ws://127.0.0.1:8765/ws', log);
  const fileSystem = new NullByteFileSystem(client, log);

  client.onEvent(event => fileSystem.handleEvent(event));
  client.onConnected(() => void fileSystem.refreshAll());   // we may have missed changes while away

  context.subscriptions.push(
    log,
    vscode.workspace.registerFileSystemProvider('nullbyte', fileSystem, { isCaseSensitive: true }),
    { dispose: () => client.dispose() });

  client.start();
  log.info('NullByte extension activated');
}