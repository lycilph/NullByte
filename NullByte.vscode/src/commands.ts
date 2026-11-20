import * as vscode from 'vscode';
import { NullByteClient } from './client';
import { rootUri } from './paths';

export function registerCommands(log: vscode.LogOutputChannel, client: NullByteClient): vscode.Disposable {
  return vscode.Disposable.from(
    vscode.commands.registerCommand('nullbyte.reconnect', () => client.reconnect()),
    
    vscode.commands.registerCommand('nullbyte.showLog', () => log.show(true)),

    vscode.commands.registerCommand('nullbyte.openWorkspace', async () => {
      const folders = vscode.workspace.workspaceFolders ?? [];

      // Already open: just bring up the explorer.
      if (folders.some(f => f.uri.scheme === 'nullbyte')) {
        await vscode.commands.executeCommand('workbench.view.explorer');
        return;
      }

      // An empty window is reused. A window with a project in it is left alone.
      await vscode.commands.executeCommand('vscode.openFolder', rootUri, { forceNewWindow: folders.length > 0 });
    }));
}