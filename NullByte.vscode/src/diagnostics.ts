import * as vscode from 'vscode';
import { Logger, NullByteClient } from './client';

const DebounceMs = 400;

const severities: Record<string, vscode.DiagnosticSeverity> = {
  error: vscode.DiagnosticSeverity.Error,
  warning: vscode.DiagnosticSeverity.Warning,
  info: vscode.DiagnosticSeverity.Information,
};

type DiagnoseResult = {
  diagnostics: { id: string; message: string; severity: string; line: number; column: number }[];
};

const isScript = (doc: vscode.TextDocument) =>
  doc.uri.scheme === 'nullbyte' && doc.uri.path.endsWith('.cx');

export class NullByteDiagnostics implements vscode.Disposable {
  private readonly collection = vscode.languages.createDiagnosticCollection('nullbyte');
  private readonly timers = new Map<string, NodeJS.Timeout>();
  private readonly subscriptions: vscode.Disposable[];

  constructor(private readonly client: NullByteClient, private readonly log: Logger) {
    this.subscriptions = [
      this.collection,
      vscode.workspace.onDidOpenTextDocument(doc => this.schedule(doc, 0)),
      vscode.workspace.onDidChangeTextDocument(e => this.schedule(e.document, DebounceMs)),
      vscode.workspace.onDidCloseTextDocument(doc => this.clear(doc)),
    ];
  }

  /** Re-check every open script, for example after (re)connecting. */
  refreshAll() { vscode.workspace.textDocuments.forEach(doc => this.schedule(doc, 0)); }

  dispose() {
    this.timers.forEach(timer => clearTimeout(timer));
    this.subscriptions.forEach(s => s.dispose());
  }

  private schedule(doc: vscode.TextDocument, delay: number) {
    if (!isScript(doc)) return;

    const key = doc.uri.toString();
    clearTimeout(this.timers.get(key));
    this.timers.set(key, setTimeout(() => {
      this.timers.delete(key);
      void this.check(doc);
    }, delay));
  }

  private async check(doc: vscode.TextDocument) {
    const version = doc.version;
    try {
      const result = await this.client.request<DiagnoseResult>('diagnose', doc.uri.path, { content: doc.getText() });
      if (doc.isClosed || doc.version !== version) return;   // outdated: a newer check is on its way

      this.collection.set(doc.uri, result.diagnostics.map(d => this.toDiagnostic(doc, d)));
      this.log.debug(`Diagnostics for ${doc.uri.path}: ${result.diagnostics.length}`);
    } catch (error) {
      // Keep whatever is shown if the game is unavailable.
      this.log.debug(`Diagnostics failed for ${doc.uri.path}: ${error instanceof Error ? error.message : error}`);
    }
  }

  private toDiagnostic(doc: vscode.TextDocument, d: DiagnoseResult['diagnostics'][number]): vscode.Diagnostic {
    // Lines and columns are 1-based. 0 means the compiler gave no location.
    const position = doc.validatePosition(new vscode.Position(Math.max(d.line, 1) - 1, Math.max(d.column, 1) - 1));

    // The compiler reports a start position only. An empty range makes VS Code underline the word there.
    const diagnostic = new vscode.Diagnostic(
      new vscode.Range(position, position), d.message, severities[d.severity] ?? vscode.DiagnosticSeverity.Error);
    diagnostic.code = d.id;
    diagnostic.source = 'NullByte';
    return diagnostic;
  }

  private clear(doc: vscode.TextDocument) {
    const key = doc.uri.toString();
    clearTimeout(this.timers.get(key));
    this.timers.delete(key);
    this.collection.delete(doc.uri);
  }
}