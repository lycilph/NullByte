import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';
import { Connection, NullByteClient } from './client';
import { NullByteFileSystem } from './filesystem';
import { NullByteDiagnostics } from './diagnostics';
import { registerCompletion } from './completion';
import { registerHover } from './hover';
import { registerSignatureHelp } from './signature';

const connectionPath = path.join(os.homedir(), '.nullbyte', 'editor.json');

function readConnection(): Connection {
  const parsed = JSON.parse(fs.readFileSync(connectionPath, 'utf8'));
  if (typeof parsed.url !== 'string' || typeof parsed.token !== 'string')
    throw new Error('editor.json must contain "url" and "token"');
  return { url: parsed.url, token: parsed.token };
}

export function activate(context: vscode.ExtensionContext) {
  const log = vscode.window.createOutputChannel('NullByte', { log: true });
  
  const client = new NullByteClient(log, readConnection);
  const fileSystem = new NullByteFileSystem(client, log);
  const diagnostics = new NullByteDiagnostics(client, log);

  client.onEvent(event => fileSystem.handleEvent(event));
  client.onConnected(() => {
    void fileSystem.refreshAll();   // we may have missed changes while away
    diagnostics.refreshAll();
  });

  context.subscriptions.push(
    registerSignatureHelp(client, log),
    registerHover(client, log),
    registerCompletion(client, log),
    diagnostics,
    log,
    vscode.workspace.registerFileSystemProvider('nullbyte', fileSystem, { isCaseSensitive: true }),
    { dispose: () => client.dispose() });

  client.start();
  log.info('NullByte extension activated');
}