import * as vscode from 'vscode';
import { Logger, NullByteClient } from './client';

type HoverResult = {
  hover: { signature: string; documentation?: string; ramCost?: number } | null;
};

export function registerHover(client: NullByteClient, log: Logger): vscode.Disposable {
  return vscode.languages.registerHoverProvider(
    { scheme: 'nullbyte', pattern: '**/*.cx' },
    {
      async provideHover(doc, position, token) {
        try {
          const result = await client.request<HoverResult>('hover', doc.uri.path, {
            content: doc.getText(),
            offset: doc.offsetAt(position),
          });
          if (token.isCancellationRequested || !result.hover) return undefined;

          const { signature, documentation, ramCost } = result.hover;
          const markdown = new vscode.MarkdownString();
          markdown.appendCodeblock(signature, 'csharp');
          if (documentation) markdown.appendText(documentation);
          if (ramCost !== undefined && ramCost !== null) {
            markdown.appendMarkdown(`${documentation ? '\n\n' : ''}**RAM cost:** ${ramCost.toFixed(2)} GB`);
          }
          return new vscode.Hover(markdown);
        } catch (error) {
          log.debug(`Hover failed: ${error instanceof Error ? error.message : error}`);
          return undefined;
        }
      },
    });
}