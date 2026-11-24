import * as vscode from 'vscode';
import { Logger, NullByteClient, RunDiagnostic  } from './client';
import { toGamePath, toUri } from './paths';

const DebounceMs = 400;

const severities: Record<string, vscode.DiagnosticSeverity> = {
  error: vscode.DiagnosticSeverity.Error,
  warning: vscode.DiagnosticSeverity.Warning,
  info: vscode.DiagnosticSeverity.Information,
};

type DiagnosticItem = {
  id: string; message: string; severity: string; line: number; column: number; file?: string | null;
  endLine: number; endColumn: number;
};
export type DiagnoseResult = { diagnostics: DiagnosticItem[]; ramCost: number | null; };
type Reported = { uri: vscode.Uri; diagnostic: vscode.Diagnostic };

const isScript = (doc: vscode.TextDocument) =>
  doc.uri.scheme === 'nullbyte' && doc.uri.path.endsWith('.cx');

export class NullByteDiagnostics implements vscode.Disposable {
  private readonly collection = vscode.languages.createDiagnosticCollection('nullbyte');
  private readonly timers = new Map<string, NodeJS.Timeout>();
    /** What each checked document reported, and for which file. Merged per file when published. */
  private readonly reported = new Map<string, Reported[]>();
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
      const path = toGamePath(doc.uri);
      if (path === undefined) return;

      const result = await this.client.request<DiagnoseResult>('diagnose', path, { content: doc.getText() });
      if (doc.isClosed || doc.version !== version) return;   // outdated: a newer check is on its way

      const host = path.split('/')[1];   // '/home/a.cx' -> 'home'
      const key = doc.uri.toString();
      const previous = this.reported.get(key) ?? [];
      const next = result.diagnostics.map(d => this.toReported(doc, host, d));

      this.reported.set(key, next);
      this.publish([doc.uri, ...previous.map(r => r.uri), ...next.map(r => r.uri)]);
      this.log.debug(`Diagnostics for ${doc.uri.path}: ${result.diagnostics.length}`);
    } catch (error) {
      // Keep whatever is shown if the game is unavailable.
      this.log.debug(`Diagnostics failed for ${doc.uri.path}: ${error instanceof Error ? error.message : error}`);
    }
  }

  /** Publish diagnostics returned when attempting to run a script. */
  reportRunDiagnostics(doc: vscode.TextDocument, diagnostics: RunDiagnostic[]) {
    const path = toGamePath(doc.uri);
    if (path === undefined) return;

    const host = path.split('/')[1];
    const key = doc.uri.toString();
    const previous = this.reported.get(key) ?? [];
    const next = diagnostics.map(d => this.toReported(doc, host, d));

    this.reported.set(key, next);
    this.publish([doc.uri, ...previous.map(r => r.uri), ...next.map(r => r.uri)]);

    this.log.debug(`Run diagnostics for ${doc.uri.path}: ${diagnostics.length}`);
  }

  private toReported(doc: vscode.TextDocument, host: string, d: DiagnosticItem): Reported {
    // A diagnostic belongs to the file the compiler named: an include, or the checked file itself.
    const uri = d.file ? toUri(`/${host}/${d.file.replace(/^\/+/, '')}`) : doc.uri;

    // Lines and columns are 1-based. 0 means the compiler gave no location.
    const start = new vscode.Position(Math.max(d.line, 1) - 1, Math.max(d.column, 1) - 1);
    const end = d.endLine > 0 && d.endColumn > 0
      ? new vscode.Position(d.endLine - 1, d.endColumn - 1)
      : start;

    // Included-file diagnostics must be validated against the target document, not the source that included it.
    const targetDoc = uri.toString() === doc.uri.toString()
      ? doc
      : vscode.workspace.textDocuments.find(open => open.uri.toString() === uri.toString());
    let range = new vscode.Range(start, end);
    if (targetDoc) range = targetDoc.validateRange(range);

    const diagnostic = new vscode.Diagnostic(
      range, d.message, severities[d.severity.toLowerCase()] ?? vscode.DiagnosticSeverity.Error);
    diagnostic.code = d.id;
    diagnostic.source = 'NullByte';
    return { uri, diagnostic };
  }

  /** Rebuild what is shown for the given files from everything the checked documents reported. */
  private publish(uris: vscode.Uri[]) {
    const unique = new Map(uris.map(uri => [uri.toString(), uri]));

    for (const [key, uri] of unique) {
      const seen = new Set<string>();
      const merged: vscode.Diagnostic[] = [];

      for (const list of this.reported.values()) {
        for (const { uri: target, diagnostic } of list) {
          if (target.toString() !== key) continue;
          const start = diagnostic.range.start;
          const identity = `${diagnostic.code}|${start.line}|${start.character}|${diagnostic.message}`;
          if (seen.has(identity)) continue;   // the same error reported from two documents
          seen.add(identity);
          merged.push(diagnostic);
        }
      }

      if (merged.length === 0) this.collection.delete(uri);
      else this.collection.set(uri, merged);
    }
  }

  private clear(doc: vscode.TextDocument) {
    const key = doc.uri.toString();
    clearTimeout(this.timers.get(key));
    this.timers.delete(key);

    const previous = this.reported.get(key) ?? [];
    this.reported.delete(key);
    this.publish([doc.uri, ...previous.map(r => r.uri)]);
  }
}