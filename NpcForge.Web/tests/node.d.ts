// Playwright runs the tests in Node, but @types/node is not one of the app's packages (ADR-017
// decision 1). The one Node call the tests make, reading the bakery run file, is typed here by hand.
declare module 'node:fs' {
  export function readFileSync(path: URL, encoding: 'utf8'): string;
}
