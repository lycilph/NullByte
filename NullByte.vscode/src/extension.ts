import * as vscode from 'vscode';
import { NullByteClient } from './client';
import { getConnectionPath, readConnection } from './connection';
import { NullByteFileSystem } from './filesystem';
import { NullByteDiagnostics } from './diagnostics';
import { registerCompletion } from './completion';
import { registerHover } from './hover';
import { registerSignatureHelp } from './signature';
import { NullByteCommands } from './commands';
import { NullByteStatusBar } from './status';
import { registerDefinition } from './definition';
import { NullByteCodeLensProvider } from './codeLens';

export function activate(context: vscode.ExtensionContext): void {
  const log = vscode.window.createOutputChannel('NullByte', { log: true });
  const client = new NullByteClient(log, readConnection);
  const fileSystem = new NullByteFileSystem(client, log);
  const diagnostics = new NullByteDiagnostics(client, log);
  const statusBar = new NullByteStatusBar(client);
  const commands = new NullByteCommands(log, client, diagnostics);

  client.onEvent(event => fileSystem.handleEvent(event));
  client.onConnected(() => {
    void fileSystem.refreshAll(); // we may have missed changes while away
    diagnostics.refreshAll();
  });

  log.info(`Connection file: ${getConnectionPath()}`);

  context.subscriptions.push(
    statusBar,
    vscode.languages.registerCodeLensProvider(
      { scheme: 'nullbyte', pattern: '**/*.cx' },
      new NullByteCodeLensProvider(),
    ),
    vscode.workspace.onDidChangeConfiguration(event => {
      if (!event.affectsConfiguration('nullbyte.connectionFile')) return;
      log.info(`Connection file is now ${getConnectionPath()}`);
      client.reconnect();
    }),
    commands.register(),
    registerSignatureHelp(client, log),
    registerHover(client, log),
    registerCompletion(client, log),
    registerDefinition(client, log),
    diagnostics,
    log,
    vscode.workspace.registerFileSystemProvider('nullbyte', fileSystem, { isCaseSensitive: true }),
    { dispose: () => client.dispose() },
  );

  client.start();
  log.info('NullByte extension activated');
}
