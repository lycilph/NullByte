import * as vscode from 'vscode';

/** VS Code labels the workspace root with the last segment of its path. */
export const rootName = 'NullByte';
const prefix = `/${RootName}`;

export const rootUri = vscode.Uri.from({ scheme: 'nullbyte', path: prefix });

/** nullbyte:/NullByte/home/a.cx becomes /home/a.cx. Undefined for anything outside the game folder. */
export function toGamePath(uri: vscode.Uri): string | undefined {
  if (uri.path === prefix) return '/';
  return uri.path.startsWith(prefix + '/') ? uri.path.slice(prefix.length) : undefined;
}

/** /home/a.cx becomes nullbyte:/NullByte/home/a.cx */
export function toUri(gamePath: string): vscode.Uri {
  return vscode.Uri.from({ scheme: 'nullbyte', path: prefix + (gamePath === '/' ? '' : gamePath) });
}