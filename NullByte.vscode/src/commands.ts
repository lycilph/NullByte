
import * as vscode from 'vscode';
import { NullByteClient } from './client';
import { NullByteDiagnostics } from './diagnostics';
import { rootUri, toGamePath } from './paths';


/** Parse shell-style arguments without performing shell expansion. */
function parseArguments(input: string): string[] {
  const args: string[] = [];
  let current = '';
  let quote: "'" | '"' | undefined;
  let escaped = false;
  let started = false;

  for (const char of input) {
    if (escaped) {
      current += char;
      escaped = false;
      started = true;
      continue;
    }

    if (char === '\\' && quote !== "'") {
      escaped = true;
      started = true;
      continue;
    }

    if (quote) {
      if (char === quote) {
        quote = undefined;
      } else {
        current += char;
      }
      started = true;
      continue;
    }

    if (char === "'" || char === '"') {
      quote = char;
      started = true;
      continue;
    }

    if (/\s/.test(char)) {
      if (started) {
        args.push(current);
        current = '';
        started = false;
      }
      continue;
    }

    current += char;
    started = true;
  }

  if (escaped) {
    throw new Error('Argument input ends with an incomplete escape.');
  }

  if (quote) {
    throw new Error(`Unclosed ${quote} quote in argument input.`);
  }

  if (started) {
    args.push(current);
  }

  return args;
}

export function registerCommands(
  log: vscode.LogOutputChannel,
  client: NullByteClient,
  diagnostics: NullByteDiagnostics
): vscode.Disposable {
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
    }),

    vscode.commands.registerCommand('nullbyte.runCurrentFile', async () => {
      const editor = vscode.window.activeTextEditor;
      if (!editor) {
        void vscode.window.showWarningMessage('No active editor.');
        return;
      }

      const doc = editor.document;
      const path = toGamePath(doc.uri);

      if (path === undefined || !path.endsWith('.cx')) {
        void vscode.window.showWarningMessage('The active file is not a NullByte script.');
        return;
      }

      try {
        const input = await vscode.window.showInputBox({
          prompt: 'Enter script arguments',
          placeHolder: 'foo "hello world" --verbose',
          ignoreFocusOut: true
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

        const result = await client.runScript(path, args);

        diagnostics.reportRunDiagnostics(doc, result.diagnostics ?? []);

        if (!result.started) {
          void vscode.window.showErrorMessage(
            result.failureMessage || 'The script could not be started.'
          );
          return;
        }

        void vscode.window.showInformationMessage(
          `Started ${path} (PID ${result.pid}).`
        );
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        log.error(`Failed to run ${path}: ${message}`);
        void vscode.window.showErrorMessage(`Failed to run ${path}: ${message}`);
      }
    })
  );
}