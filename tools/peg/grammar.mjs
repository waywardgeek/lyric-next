// Execute the canonical grammar without maintaining a second syntax rule set.
// Peggy consumes strings. Each ASCII position stands for an immutable token view;
// >> has two views, permitting contextual type closes without changing the lexer.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import peggy from 'peggy';

export const root = fileURLToPath(new URL('../../', import.meta.url));
const grammarPath = path.join(root, 'lyric-grammar.peg');

// Hooks describe consumption only during structural grammar validation. They
// are NOT a replacement lexer and that parser is never used to accept source.
const hooks = new Set([
    'XIDStart', 'XIDContinue', 'Digit', 'HexDigit', 'ByteChar', 'StringChar',
    'TripleChar', 'SymbolChar', 'FStringChar', 'CommentChar', 'HorizontalSpace',
    'NL', 'EOF',
]);

function readRules(source) {
    const rules = new Map();
    for (const [index, line] of source.split(/\r?\n/).entries()) {
        if (!line.trim() || line.trimStart().startsWith('#')) {
            continue;
        }
        const match = /^([A-Za-z][A-Za-z0-9]*) <- (.+)$/.exec(line);
        if (!match || rules.has(match[1])) {
            throw new Error(`Invalid or duplicate rule on canonical line ${index + 1}.`);
        }
        rules.set(match[1], match[2]);
    }
    return rules;
}

// Translate only notation, never syntax policy. Quoted terminals must be scanned
// before hooks/operators so their characters cannot become grammar instructions.
function translate(expression, terminal) {
    let result = '';
    for (let i = 0; i < expression.length;) {
        const quote = expression[i];
        if (quote === '"' || quote === "'") {
            let value = '';
            i += 1;
            while (i < expression.length && expression[i] !== quote) {
                let character = expression[i++];
                if (character === '\\') {
                    const escaped = expression[i++];
                    const escapes = { n: '\n', r: '\r', t: '\t', '\\': '\\', '"': '"', "'": "'" };
                    if (!Object.hasOwn(escapes, escaped)) {
                        throw new Error('Unsupported escape in canonical grammar terminal.');
                    }
                    character = escapes[escaped];
                }
                value += character;
            }
            if (expression[i++] !== quote) {
                throw new Error('Unterminated canonical grammar terminal.');
            }
            result += terminal(value);
        } else if (expression[i] === '@') {
            const match = /^@([A-Za-z]+)/.exec(expression.slice(i));
            if (!match || !hooks.has(match[1])) {
                throw new Error('Unknown canonical lexical hook.');
            }
            result += `Hook_${match[1]}`;
            i += match[0].length;
        } else {
            result += expression[i++];
        }
    }
    return result;
}

function kindRule(kind, extra = 'true') {
    return `&{ const view = options.views[offset()]; const token = view?.token; return view?.part === null && token?.kind === ${JSON.stringify(kind)} && (${extra}); } .`;
}

export function compileGrammar(source = fs.readFileSync(grammarPath, 'utf8')) {
    const rules = readRules(source);
    const hookRules = [...hooks].map(name => `Hook_${name} = ${name === 'EOF' ? '!.' : '.'}`).join('\n');
    const structural = [...rules].map(([name, rhs]) => `${name} = ${translate(rhs, JSON.stringify)}`).join('\n');
    // This checks the original rule graph, including lexical rules later replaced
    // at the token boundary: unknown rules, left recursion, nullable loops, etc.
    peggy.generate(`${structural}\n${hookRules}`, { allowedStartRules: ['File'] });

    const terminals = new Map();
    function terminal(text) {
        if (!terminals.has(text)) {
            terminals.set(text, `Terminal${terminals.size}`);
        }
        return terminals.get(text);
    }
    const replacements = new Map([
        ['Ident', kindRule('identifier')],
        ['IntegerLiteral', kindRule('integer')],
        ['FloatLiteral', kindRule('float')],
        ['ByteLiteral', kindRule('byte')],
        ['StringLiteral', kindRule('string', '!token.text.startsWith(\'"""\')')],
        ['TripleStringLiteral', kindRule('string', 'token.text.startsWith(\'"""\')')],
        ['SymbolLiteral', kindRule('symbol')],
        ['FStringText', kindRule('string-text')],
        ['TupleIndex', kindRule('tuple-index')],
        // TypeClose is the ONLY single-character consumer of a >> view. Other
        // terminals must see a whole token, except >> itself which consumes two.
        ['TypeClose', '&{ const token = options.views[offset()]?.token; return token?.kind === "punctuation" && (token.text === ">" || token.text === ">>"); } .'],
        ['NL', kindRule('newline')],
        ['EOF', `${kindRule('end')} !.`],
    ]);
    for (const name of replacements.keys()) {
        if (!rules.has(name)) {
            throw new Error(`Token boundary rule ${name} is missing from canonical grammar.`);
        }
    }
    const adapted = [...rules].map(([name, rhs]) => {
        // Named lexical rules make predicate failure visible to Peggy's expected-
        // token diagnostics. Do not name whole syntax productions: that would
        // hide the deeper terminal failure behind a broad File/Expr label.
        const label = replacements.has(name) ? ` ${JSON.stringify(name)}` : '';
        return `${name}${label} = ${replacements.get(name) ?? translate(rhs, terminal)}`;
    });
    for (const [text, name] of terminals) {
        // Peggy balances braces in embedded code; keep literal braces out of
        // predicate source even when they would occur inside JavaScript strings.
        const spelling = JSON.stringify(text).replaceAll('{', '\\u007B').replaceAll('}', '\\u007D');
        if (text === '>>') {
            adapted.push(`${name} ${JSON.stringify(text)} = &{ const first = options.views[offset()]; const second = options.views[offset() + 1]; return first?.part === 0 && second?.part === 1 && first.token === second.token; } . .`);
        } else {
            adapted.push(`${name} ${JSON.stringify(text)} = &{ const view = options.views[offset()]; const token = view?.token; return view?.part === null && token?.text === ${spelling} && ['punctuation', 'keyword', 'identifier', 'discard', 'fstring-start', 'fstring-end', 'interpolation-start', 'interpolation-end'].includes(token.kind); } .`);
        }
    }
    adapted.push('CheckExpr = SepOpt Expr SepOpt EOF');
    const parser = peggy.generate(`${adapted.join('\n')}\n${hookRules}`, {
        allowedStartRules: ['File', 'CheckExpr'],
    });
    return { parser, ruleCount: rules.size };
}

export function lexFile(filename, dotnet = process.env.DOTNET || 'dotnet') {
    const assembly = path.join(root, 'compiler/bin/Release/net10.0/Lyric.dll');
    const result = spawnSync(dotnet, [assembly, '--inspect-tokens', filename], {
        encoding: 'utf8', timeout: 30000, maxBuffer: 32 * 1024 * 1024,
    });
    if (result.error) {
        throw result.error;
    }
    if (result.status !== 0) {
        const error = new Error(result.stderr.trim() || 'Lexer failed without a diagnostic.');
        error.sourceRejected = result.status === 1 && /error LYC\d+:/.test(result.stderr);
        throw error;
    }
    if (result.stderr) {
        throw new Error(`Unexpected lexer stderr: ${result.stderr}`);
    }
    const tokens = result.stdout.trimEnd().split('\n').map(line => JSON.parse(line));
    if (tokens.at(-1)?.kind !== 'end') {
        throw new Error('Lexer did not emit an end token.');
    }
    return tokens;
}

export function recognize(parser, tokens, expression = false) {
    // No copied/replaced tokens and no mutable split state: Peggy backtracking
    // restores its character position, which restores the view position too.
    const views = [];
    for (const token of tokens) {
        if (token.kind === 'punctuation' && token.text === '>>') {
            views.push({ token, part: 0 });
            views.push({ token, part: 1 });
        } else {
            views.push({ token, part: null });
        }
    }
    try {
        return parser.parse('x'.repeat(views.length), {
            views, startRule: expression ? 'CheckExpr' : 'File',
        });
    } catch (error) {
        if (error.name === 'SyntaxError' && error.location) {
            const view = views[error.location.start.offset] ?? views.at(-1);
            const delta = view.part ?? 0;
            // Each half is one ASCII byte/scalar. Other tokens use their original
            // start, including the zero-width EOF token. Never alter lexer data.
            error.sourcePosition = {
                text: view.part === null ? view.token.text : '>',
                byteOffset: view.token.byteOffset + delta,
                line: view.token.line,
                column: view.token.column + delta,
            };
        }
        throw error;
    }
}
