import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { NullByteClient } from './client';
import { NullByteFileSystem } from './filesystem';
import { NullByteDiagnostics } from './diagnostics';

export function activate(context: vscode.ExtensionContext) {
  const log = vscode.window.createOutputChannel('NullByte', { log: true });
  
  const extensionRoot = context.extensionPath;
  const tokenPath = path.join(extensionRoot, '../NullByte.ConsoleUI/bin/Debug/net10.0/.nullbyte', 'editor-token');
  log.info('Looking for token at: ' + tokenPath);
  const readToken = () => fs.readFileSync(tokenPath, 'utf8').trim();
  
  const client = new NullByteClient('ws://127.0.0.1:8765/ws', log, readToken);
  const fileSystem = new NullByteFileSystem(client, log);
  const diagnostics = new NullByteDiagnostics(client, log);

  client.onEvent(event => fileSystem.handleEvent(event));
  client.onConnected(() => {
    void fileSystem.refreshAll();   // we may have missed changes while away
    diagnostics.refreshAll();
  });

  context.subscriptions.push(
    diagnostics,
    log,
    vscode.workspace.registerFileSystemProvider('nullbyte', fileSystem, { isCaseSensitive: true }),
    { dispose: () => client.dispose() });

  client.start();
  log.info('NullByte extension activated');
}