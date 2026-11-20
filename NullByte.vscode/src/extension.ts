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

const defaultConnectionPath = path.join(os.homedir(), '.nullbyte', 'editor.json');

function connectionPath(): string {
  const configured = vscode.workspace.getConfiguration('nullbyte').get<string>('connectionFile', '').trim();
  if (!configured) return defaultConnectionPath;

  const expanded = configured === '~' || configured.startsWith('~/') || configured.startsWith('~\\')
    ? path.join(os.homedir(), configured.slice(1))
    : configured;
  return path.resolve(expanded);
}

function readConnection(): Connection {
  const parsed = JSON.parse(fs.readFileSync(connectionPath(), 'utf8'));
  if (typeof parsed.url !== 'string' || typeof parsed.token !== 'string')
    throw new Error('The connection file must contain "url" and "token"');

  return { url: parsed.url, token: parsed.token, pid: typeof parsed.pid === 'number' ? parsed.pid : undefined };
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

  log.info(`Connection file: ${connectionPath()}`);

  context.subscriptions.push(
    vscode.workspace.onDidChangeConfiguration(e => {
      if (!e.affectsConfiguration('nullbyte.connectionFile')) return;
      log.info(`Connection file is now ${connectionPath()}`);
      client.reconnect();
    }),
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