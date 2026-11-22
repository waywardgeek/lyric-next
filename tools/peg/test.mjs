// Public developer fixtures for the executable grammar; no fixture answers are
// obtained from the hand-written parser. Lexical rejection is a distinct phase.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import assert from 'node:assert/strict';
import { compileGrammar, lexFile, recognize, root } from './grammar.mjs';

const source = fs.readFileSync(path.join(root, 'lyric-grammar.peg'), 'utf8');
const { parser, ruleCount } = compileGrammar(source);
console.log(`Peggy compiled all ${ruleCount} canonical rules and the token adapter.`);

// Prove grammar checking is active rather than reporting a ceremonial PASS.
assert.throws(() => compileGrammar(source + '\nBroken <- MissingRule\n'), /not defined/);
assert.throws(() => compileGrammar(source + '\nBroken <- Broken "x"\n'), /recursion/i);
assert.throws(() => compileGrammar(source + '\nBroken <- ("")*\n'), /infinite|consume/i);
assert.throws(() => compileGrammar(source + '\nBroken <- @Unknown\n'), /Unknown/);
console.log('PASS grammar-check negative controls');

// Optional public fixture file lets the hand-written parser and PEG checker
// test the same authored expectations without deriving answers from each other.
const fixturePath = process.argv[2] ?? new URL('./cases.json', import.meta.url);
const cases = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'lyric-peg-'));
let failures = 0;
try {
    for (const fixture of cases) {
        const filename = path.join(directory, `${fixture.id}.ly`);
        fs.writeFileSync(filename, fixture.source);
        let accepted = false;
        let phase = 'lexer';
        let rejection = '';
        let errorByte;
        try {
            const tokens = lexFile(filename);
            // Recognition must not replace, mutate or consume the lexer records.
            for (const token of tokens) {
                Object.freeze(token);
            }
            Object.freeze(tokens);
            phase = 'syntax';
            recognize(parser, tokens, fixture.expression);
            accepted = true;
        } catch (error) {
            // Crashes, tool failures and arbitrary exceptions are not valid
            // negative cases. Only located PEG errors or lexical diagnostics count.
            if (!(phase === 'lexer' && error.sourceRejected) &&
                !(phase === 'syntax' && error.name === 'SyntaxError' && error.location)) {
                throw error;
            }
            rejection = error.message;
            errorByte = error.sourcePosition?.byteOffset;
        }
        const passed = accepted === fixture.accept &&
            (accepted || phase === (fixture.phase ?? 'syntax')) &&
            (fixture.errorByte === undefined || errorByte === fixture.errorByte);
        console.log(`${passed ? 'PASS' : 'FAIL'} ${fixture.id}${passed ? '' : `: ${phase}: ${rejection}`}`);
        if (!passed) {
            failures += 1;
        }
    }
} finally {
    fs.rmSync(directory, { recursive: true, force: true });
}
console.log(`${cases.length - failures}/${cases.length} grammar fixtures passed`);
process.exitCode = failures === 0 ? 0 : 1;
