// Developer syntax check, not ordinary compilation or semantic validation.
import { compileGrammar, lexFile, recognize } from './grammar.mjs';

const args = process.argv.slice(2);
if (args.length !== 1) {
    console.error('Usage: node tools/peg/check.mjs FILE.ly');
    process.exitCode = 2;
} else {
    let tokens;
    try {
        const { parser } = compileGrammar();
        tokens = lexFile(args[0]);
        recognize(parser, tokens);
        console.log(`Syntax accepted: ${JSON.stringify(args[0])}`);
    } catch (error) {
        if (error.sourcePosition) {
            const token = error.sourcePosition;
            console.error(`${JSON.stringify(args[0])}:${token.line}:${token.column}: PEG syntax rejected at ${JSON.stringify(token.text)} (byte ${token.byteOffset}).`);
            process.exitCode = 1;
        } else {
            console.error(error.message);
            process.exitCode = error.sourceRejected ? 1 : 2;
        }
    }
}
