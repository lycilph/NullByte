import * as vscode from 'vscode';
import { Logger, NullByteClient } from './client';

type SignatureHelpResult = {
  help: {
    activeSignature: number;
    signatures: {
      label: string;
      documentation?: string;
      ramCost?: number;
      activeParameter: number;
      parameters: { label: string; documentation?: string }[];
    }[];
  } | null;
};

export function registerSignatureHelp(client: NullByteClient, log: Logger): vscode.Disposable {
  return vscode.languages.registerSignatureHelpProvider(
    { scheme: 'nullbyte', pattern: '**/*.cx' },
    {
      async provideSignatureHelp(doc, position, token, context) {
        try {
          const result = await client.request<SignatureHelpResult>('signatureHelp', doc.uri.path, {
            content: doc.getText(),
            offset: doc.offsetAt(position),
          });
          if (token.isCancellationRequested || !result.help) return undefined;

          const help = new vscode.SignatureHelp();
          help.signatures = result.help.signatures.map(s => {
            const markdown = new vscode.MarkdownString();
            if (s.documentation) markdown.appendText(s.documentation);
            if (s.ramCost !== undefined) {
              markdown.appendMarkdown(`${s.documentation ? '\n\n' : ''}**RAM cost:** ${s.ramCost.toFixed(2)} GB`);
            }

            const info = new vscode.SignatureInformation(s.label, markdown.value ? markdown : undefined);
            info.parameters = s.parameters.map(p => new vscode.ParameterInformation(p.label, p.documentation));
            info.activeParameter = s.activeParameter;
            return info;
          });

          // Keep the overload the user paged to, as long as it is still a candidate.
          const previous = context.activeSignatureHelp?.signatures[context.activeSignatureHelp.activeSignature]?.label;
          const kept = previous === undefined ? -1 : help.signatures.findIndex(s => s.label === previous);
          help.activeSignature = kept >= 0 ? kept : result.help.activeSignature;
          help.activeParameter = result.help.signatures[help.activeSignature]?.activeParameter ?? 0;

          log.debug(`Signature help: ${help.signatures.length} overload(s), parameter ${help.activeParameter}`);
          return help;
        } catch (error) {
          log.debug(`Signature help failed: ${error instanceof Error ? error.message : error}`);
          return undefined;
        }
      },
    },
    { triggerCharacters: ['(', ','], retriggerCharacters: [')'] });
}