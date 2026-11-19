import * as vscode from 'vscode';
import { NullByteClient } from './client';
import { NullByteFileSystem } from './filesystem';

export function activate(context: vscode.ExtensionContext) {
  const client = new NullByteClient('ws://127.0.0.1:8765/ws');

  context.subscriptions.push(
    vscode.workspace.registerFileSystemProvider('nullbyte', new NullByteFileSystem(client),
      { isCaseSensitive: true, isReadonly: true }),
    { dispose: () => client.dispose() });
}