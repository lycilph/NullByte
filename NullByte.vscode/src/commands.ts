import * as vscode from 'vscode';
import { NullByteClient } from './client';
import { NullByteDiagnostics, DiagnoseResult } from './diagnostics';
import { parseArguments } from './argumentParser';
import { rootUri, toGamePath } from './paths';

export class NullByteCommands {
  constructor(
    private readonly log: vscode.LogOutputChannel,
    private readonly client: NullByteClient,
    private readonly diagnostics: NullByteDiagnostics,
  ) {}

  register(): vscode.Disposable {
    return vscode.Disposable.from(
      vscode.commands.registerCommand('nullbyte.reconnect', () => this.reconnect()),
      vscode.commands.registerCommand('nullbyte.showLog', () => this.showLog()),
      vscode.commands.registerCommand('nullbyte.openWorkspace', () => this.openWorkspace()),
      vscode.commands.registerCommand('nullbyte.runCurrentFile', () => this.runCurrentFile()),
      vscode.commands.registerCommand('nullbyte.getRamCost', () => this.getRamCost()),
    );
  }

  private reconnect(): void {
    this.client.reconnect();
  }

  private showLog(): void {
    this.log.show(true);
  }

  private async openWorkspace(): Promise<void> {
    const folders = vscode.workspace.workspaceFolders ?? [];

    // Already open: just bring up the explorer.
    if (folders.some(folder => folder.uri.scheme === 'nullbyte')) {
      await vscode.commands.executeCommand('workbench.view.explorer');
      return;
    }

    // An empty window is reused. A window with a project in it is left alone.
    await vscode.commands.executeCommand('vscode.openFolder', rootUri, { forceNewWindow: folders.length > 0 });
  }

  private async runCurrentFile(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
      void vscode.window.showWarningMessage('No active editor.');
      return;
    }

    const doc = editor.document;
    const gamePath = toGamePath(doc.uri);

    if (gamePath === undefined || !gamePath.endsWith('.cx')) {
      void vscode.window.showWarningMessage('The active file is not a NullByte script.');
      return;
    }

    try {
      const input = await vscode.window.showInputBox({
        prompt: 'Enter script arguments',
        placeHolder: 'foo "hello world" --verbose',
        ignoreFocusOut: true,
      });

      // Escape or cancellation: do not run the script.
      if (input === undefined) return;

      let args: string[];
      try {
        args = parseArguments(input);
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        void vscode.window.showErrorMessage(`Invalid arguments: ${message}`);
        return;
      }

      // Always run the saved version of the script.
      if (!await doc.save()) {
        void vscode.window.showWarningMessage('Could not save the script. Run cancelled.');
        return;
      }

      const result = await this.client.runScript(gamePath, args);
      this.diagnostics.reportRunDiagnostics(doc, result.diagnostics ?? []);

      if (!result.started) {
        void vscode.window.showErrorMessage(
          result.failureMessage || 'The script could not be started.',
        );
        return;
      }

      void vscode.window.showInformationMessage(`Started ${gamePath} (PID ${result.pid}).`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.log.error(`Failed to run ${gamePath}: ${message}`);
      void vscode.window.showErrorMessage(`Failed to run ${gamePath}: ${message}`);
    }
  }

  private async getRamCost(): Promise<void> {
    const editor = vscode.window.activeTextEditor;

    if (!editor) {
      void vscode.window.showErrorMessage('No active editor.');
      return;
    }

    const doc = editor.document;

    if (doc.uri.scheme !== 'nullbyte' || !doc.fileName.endsWith('.cx')) {
      void vscode.window.showErrorMessage('The active file is not a NullByte script.');
      return;
    }

    const gamePath = toGamePath(doc.uri);
    if (gamePath === undefined) {
      void vscode.window.showErrorMessage('Unable to resolve the script path.');
      return;
    }

    try {
      const result = await this.client.request<DiagnoseResult>('diagnose', gamePath, { content: doc.getText() });
      if (doc.isClosed) return; // outdated: a newer check is on its way

      // Update the Problems collection with the latest compiler diagnostics.
      this.diagnostics.reportRunDiagnostics(doc, result.diagnostics);

      if (result.diagnostics.some(diagnostic => diagnostic.severity.toLowerCase() === 'error')) {
        void vscode.window.showErrorMessage(
          'Unable to calculate RAM cost. Check the Problems panel for compilation errors.',
        );
        return;
      }

      if (result.ramCost === null) {
        void vscode.window.showWarningMessage('RAM cost is not available.');
        return;
      }

      void vscode.window.showInformationMessage(`RAM cost: ${result.ramCost.toFixed(2)} GB`);
    } catch (error) {
      void vscode.window.showErrorMessage(
        `Failed to calculate RAM cost: ${error instanceof Error ? error.message : String(error)}`,
      );
    }
  }
}
