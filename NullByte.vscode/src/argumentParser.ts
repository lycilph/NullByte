/** Parse shell-style arguments without performing shell expansion. */
export function parseArguments(input: string): string[] {
  const args: string[] = [];
  let current = '';
  let quote: "'" | '"' | undefined;
  let escaped = false;
  let started = false;

  for (const char of input) {
    if (escaped) {
      current += char;
      escaped = false;
      started = true;
      continue;
    }

    if (char === '\\' && quote !== "'") {
      escaped = true;
      started = true;
      continue;
    }

    if (quote) {
      if (char === quote) {
        quote = undefined;
      } else {
        current += char;
      }
      started = true;
      continue;
    }

    if (char === "'" || char === '"') {
      quote = char;
      started = true;
      continue;
    }

    if (/\s/.test(char)) {
      if (started) {
        args.push(current);
        current = '';
        started = false;
      }
      continue;
    }

    current += char;
    started = true;
  }

  if (escaped) {
    throw new Error('Argument input ends with an incomplete escape.');
  }

  if (quote) {
    throw new Error(`Unclosed ${quote} quote in argument input.`);
  }

  if (started) {
    args.push(current);
  }

  return args;
}
