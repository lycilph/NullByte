import * as vscode from 'vscode';
import { ConnectionState, NullByteClient } from './client';

const icons: Record<ConnectionState['status'], string> = {
  connected: '$(plug)',
  connecting: '$(sync~spin)',
  disconnected: '$(debug-disconnect)',
};

export class NullByteStatusBar implements vscode.Disposable {
  private readonly item = vscode.window.createStatusBarItem('nullbyte.status', vscode.StatusBarAlignment.Left, 0);

  constructor(client: NullByteClient) {
    this.item.name = 'NullByte';
    this.item.command = 'nullbyte.showLog';
    this.render(client.state);
    client.onStateChange(state => this.render(state));
    this.item.show();
  }

  dispose() { this.item.dispose(); }

  private render(state: ConnectionState) {
    this.item.text = `${icons[state.status]} NullByte`;

    const tooltip = new vscode.MarkdownString();
    tooltip.appendMarkdown('**NullByte**\n\n');
    tooltip.appendText(state.detail);
    tooltip.appendMarkdown('\n\n*Click to show the log.*');
    this.item.tooltip = tooltip;
  }
}