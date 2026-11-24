import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';
import { Connection } from './client';

const defaultConnectionPath = path.join(os.homedir(), '.nullbyte', 'editor.json');

/** Resolve the configured connection file, expanding a leading home-directory marker. */
export function getConnectionPath(): string {
  const configured = vscode.workspace.getConfiguration('nullbyte').get<string>('connectionFile', '').trim();
  if (!configured) return defaultConnectionPath;

  const expanded = configured === '~' || configured.startsWith('~/') || configured.startsWith('~\\')
    ? path.join(os.homedir(), configured.slice(1))
    : configured;
  return path.resolve(expanded);
}

/** Read and validate the connection details written by the game. */
export function readConnection(): Connection {
  const parsed = JSON.parse(fs.readFileSync(getConnectionPath(), 'utf8'));
  if (typeof parsed.url !== 'string' || typeof parsed.token !== 'string') {
    throw new Error('The connection file must contain "url" and "token"');
  }

  return {
    url: parsed.url,
    token: parsed.token,
    pid: typeof parsed.pid === 'number' ? parsed.pid : undefined,
  };
}
