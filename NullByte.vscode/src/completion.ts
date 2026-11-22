import * as vscode from 'vscode';
import { Logger, NullByteClient } from './client';
import { toGamePath } from './paths';

type CompleteResult = {
  items: { label: string; insertText: string; kind: string; sortText: string; ramCost?: number; documentation?: string }[];
};

// Roslyn's WellKnownTags to VS Code's kinds.
const kinds: Record<string, vscode.CompletionItemKind> = {
  Method: vscode.CompletionItemKind.Method,
  ExtensionMethod: vscode.CompletionItemKind.Method,
  Property: vscode.CompletionItemKind.Property,
  Field: vscode.CompletionItemKind.Field,
  Event: vscode.CompletionItemKind.Event,
  Constant: vscode.CompletionItemKind.Constant,
  Enum: vscode.CompletionItemKind.Enum,
  EnumMember: vscode.CompletionItemKind.EnumMember,
  Class: vscode.CompletionItemKind.Class,
  Structure: vscode.CompletionItemKind.Struct,
  Interface: vscode.CompletionItemKind.Interface,
  Delegate: vscode.CompletionItemKind.Function,
  Namespace: vscode.CompletionItemKind.Module,
  Keyword: vscode.CompletionItemKind.Keyword,
  Local: vscode.CompletionItemKind.Variable,
  Parameter: vscode.CompletionItemKind.Variable,
  RangeVariable: vscode.CompletionItemKind.Variable,
  TypeParameter: vscode.CompletionItemKind.TypeParameter,
};

export function registerCompletion(client: NullByteClient, log: Logger): vscode.Disposable {
  return vscode.languages.registerCompletionItemProvider(
    { scheme: 'nullbyte', pattern: '**/*.cx' },
    {
      async provideCompletionItems(doc, position, token) {
        try {
          const path = toGamePath(doc.uri);
          if (path === undefined) return undefined;
          const result = await client.request<CompleteResult>('complete', path, {   // 'hover' / 'signatureHelp' in the others
            content: doc.getText(),
            offset: doc.offsetAt(position),
          });
          if (token.isCancellationRequested) return undefined;

          log.debug(`Completion at ${position.line + 1}:${position.character + 1}: ${result.items.length} items`);
          return result.items.map(i => {
            const item = new vscode.CompletionItem(
              { label: i.label, description: i.ramCost === null || i.ramCost === undefined ? undefined : `${i.ramCost.toFixed(2)} GB` },
              kinds[i.kind] ?? vscode.CompletionItemKind.Text);
            item.insertText = i.insertText;
            item.filterText = i.insertText;   // the label can carry a suffix such as "<>"
            item.sortText = i.sortText;
            if (i.documentation || (i.ramCost !== undefined && i.ramCost != null)) {
              const markdown = new vscode.MarkdownString();
              if (i.documentation) markdown.appendText(i.documentation);
              if (i.ramCost !== undefined && i.ramCost != null) {
                markdown.appendMarkdown(`${i.documentation ? '\n\n' : ''}**RAM cost:** ${i.ramCost.toFixed(2)} GB`);
              }
              item.documentation = markdown;
            }
            return item;
          });
        } catch (error) {
          log.debug(`Completion failed: ${error instanceof Error ? error.message : error}`);
          return undefined;
        }
      },
    },
    '.');
}