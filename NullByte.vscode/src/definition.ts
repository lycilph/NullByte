import * as vscode from 'vscode';
import { Logger, NullByteClient } from './client';
import { toGamePath, toUri } from './paths';

type DefinitionResult = { definition: { file?: string | null; line: number; column: number } | null };

export function registerDefinition(client: NullByteClient, log: Logger): vscode.Disposable {
  return vscode.languages.registerDefinitionProvider(
    { scheme: 'nullbyte', pattern: '**/*.cx' },
    {
      async provideDefinition(doc, position, token) {
        try {
          const path = toGamePath(doc.uri);
          if (path === undefined) return undefined;

          const result = await client.request<DefinitionResult>('definition', path, {
            content: doc.getText(),
            offset: doc.offsetAt(position),
          });
          if (token.isCancellationRequested || !result.definition) return undefined;

          const { file, line, column } = result.definition;
          const host = path.split('/')[1];   // '/home/a.cx' -> 'home'
          const uri = file ? toUri(`/${host}/${file.replace(/^\/+/, '')}`) : doc.uri;
          return new vscode.Location(uri, new vscode.Position(Math.max(line, 1) - 1, Math.max(column, 1) - 1));
        } catch (error) {
          log.debug(`Definition failed: ${error instanceof Error ? error.message : error}`);
          return undefined;
        }
      },
    });
}