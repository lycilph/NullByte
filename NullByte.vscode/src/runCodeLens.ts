import * as vscode from 'vscode';

export class NullByteRunCodeLensProvider implements vscode.CodeLensProvider {
  provideCodeLenses(document: vscode.TextDocument): vscode.CodeLens[] {
    if (document.uri.scheme !== 'nullbyte' || !document.uri.path.endsWith('.cx')) {
      return [];
    }

    const lens = new vscode.CodeLens(new vscode.Range(0, 0, 0, 0), {
      title: '$(play) Run Script',
      command: 'nullbyte.runCurrentFile',
      tooltip: 'Save and run this NullByte script'
    });

    const ram = new vscode.CodeLens(new vscode.Range(0, 0, 0, 0), {
      title: '$(play) Ram Cost',
      command: 'nullbyte.getRamCost',
      tooltip: 'Get ram cost of this NullByte script'
    });

    return [lens, ram];
  }
}