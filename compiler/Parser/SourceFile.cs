using System;

namespace Lyric;

internal sealed partial class SourceFile
{
    internal partial Diagnostic? Parse(bool expressionOnly)
    {
        Diagnostic? lexicalError = Lex();
        if (lexicalError is not null)
        {
            return lexicalError;
        }

        int cursor = 0;
        bool secondClose = false;
        int recursion = 0;

        Token Current() => Tokens[cursor];
        string Text() => secondClose ? ">" : TokenText(cursor);
        bool At(string text) => !secondClose && Text() == text;
        bool Kind(string kind) => !secondClose && Current().Kind == kind;
        bool Separator() => Kind("newline") || At(";");

        SyntaxFailure Failure(string message, string code = "LYC201")
        {
            Token token = Current();
            int extra = secondClose ? 1 : 0;
            return new SyntaxFailure(new Diagnostic(code, message,
                token.ByteOffset + extra, token.Line, token.Column + extra));
        }

        void Enter()
        {
            recursion += 1;
            if (recursion > 128)
            {
                throw Failure("Parser nesting limit exceeded.", "LYC203");
            }
        }

        int Take()
        {
            if (secondClose || Kind("end"))
            {
                throw Failure("Expected a complete token.");
            }
            int taken = cursor;
            cursor += 1;
            return taken;
        }

        int Expect(string spelling)
        {
            if (!At(spelling))
            {
                throw Failure($"Expected '{spelling}'.");
            }
            return Take();
        }

        int Name()
        {
            if (!Kind("identifier"))
            {
                throw Failure("Expected an identifier.");
            }
            return Take();
        }

        void CloseType()
        {
            if (secondClose)
            {
                secondClose = false;
                cursor += 1;
            }
            else if (At(">>"))
            {
                secondClose = true;
            }
            else
            {
                Expect(">");
            }
        }

        void SkipSeparators()
        {
            while (Separator())
            {
                Take();
            }
        }

        SyntaxNode Node(SyntaxKind kind, int token = -1, params SyntaxNode[] children)
        {
            // Tokens are temporary parsing inputs. The persistent spelling
            // anchor is a byte span in the source, not a disguised token reference.
            int offset = token < 0 ? -1 : Tokens[token].ByteOffset;
            int length = token < 0 ? 0 : Tokens[token].ByteLength;
            SyntaxNode node = new(this, kind, offset, length);
            foreach (SyntaxNode child in children)
            {
                node.AddChild(child);
            }
            return node;
        }

        // Type parsing is also used speculatively before comparisons. It never
        // resolves a name: a syntactically successful generic suffix stays generic.
        SyntaxNode Type(bool union = true)
        {
            Enter();
            try
            {
                SyntaxNode? prefix = null;
                if (At("ref") || At("gen"))
                {
                    prefix = Attempt(() =>
                    {
                        int operation = Take();
                        return Node(SyntaxKind.TypePrefix, operation, Type(false));
                    });
                }
                SyntaxNode result = prefix ?? OptionalType();

                if (union && At("|"))
                {
                    result = Node(SyntaxKind.UnionType, -1, result);
                    while (At("|"))
                    {
                        Take();
                        result.AddChild(Type(false));
                    }
                }
                return result;
            }
            finally
            {
                recursion -= 1;
            }
        }

        SyntaxNode FunctionType()
        {
            Expect("fn");
            Expect("(");
            SyntaxNode result = Node(SyntaxKind.FunctionType);
            if (!At(")"))
            {
                do
                {
                    result.AddChild(Type());
                    if (!At(","))
                    {
                        break;
                    }
                    Take();
                } while (!At(")"));
            }
            Expect(")");
            Expect("->");
            result.AddChild(Node(SyntaxKind.ResultType, -1, Type()));
            return result;
        }

        SyntaxNode OptionalType()
        {
            SyntaxNode? function = At("fn") ? Attempt(FunctionType) : null;
            SyntaxNode result;
            if (function is not null)
            {
                result = function;
            }
            else if (At("typeof"))
            {
                int operation = Take();
                Expect("(");
                result = Node(SyntaxKind.TypeOf, operation, Expression());
                Expect(")");
            }
            else if (At("["))
            {
                Take();
                result = Node(SyntaxKind.ArrayType, -1, Type());
                Expect("]");
            }
            else if (At("("))
            {
                Take();
                SyntaxNode first = Type();
                // Preserve the comma only for the new one-element tuple shape.
                // Older grouping and multi-element inspection trees stay stable.
                int singleTupleComma = At(",") ? cursor : -1;
                if (At(","))
                {
                    Take();
                    if (At(")"))
                    {
                        result = Node(SyntaxKind.TypeGroup, singleTupleComma, first);
                    }
                    else
                    {
                        result = Node(SyntaxKind.TypeGroup, -1, first, Type());
                    }
                    while (At(","))
                    {
                        Take();
                        if (At(")"))
                        {
                            break;
                        }
                        result.AddChild(Type());
                    }
                }
                else
                {
                    result = Node(SyntaxKind.TypeGroup, -1, first);
                }
                Expect(")");
            }
            else
            {
                result = NamedType();
            }
            if (At("?"))
            {
                Take();
                result = Node(SyntaxKind.OptionalType, -1, result);
            }
            return result;
        }

        SyntaxNode TypeArguments(bool roles)
        {
            Expect("<");
            SyntaxNode arguments = Node(SyntaxKind.TypeArguments);
            do
            {
                SyntaxNode argument = Type();
                if (roles && At(":"))
                {
                    Take();
                    argument = Node(SyntaxKind.RoleBinding, Name(), argument);
                }
                arguments.AddChild(argument);
                if (!At(","))
                {
                    break;
                }
                Take();
            } while (true);
            CloseType();
            return arguments;
        }

        // Ordered alternatives use the same rollback boundary as generic
        // speculation. Only syntax mismatches are caught; resource failures and
        // programming errors must not masquerade as another valid alternative.
        SyntaxNode? Attempt(Func<SyntaxNode> parse)
        {
            int savedCursor = cursor;
            bool savedHalf = secondClose;
            int savedCount = SyntaxNodes.Count;
            try
            {
                return parse();
            }
            catch (SyntaxFailure failure) when (failure.Diagnostic.Code == "LYC201")
            {
                ClearSyntax(savedCount);
                cursor = savedCursor;
                secondClose = savedHalf;
                return null;
            }
        }

        // A failed generic attempt leaves both cursor and model unchanged. No
        // type lookup participates in this choice, even when names are unknown.
        SyntaxNode? TryGeneric()
        {
            if (!At("<"))
            {
                return null;
            }
            return Attempt(() =>
            {
                SyntaxNode arguments = TypeArguments(true);
                if (!At("(") && !At("."))
                {
                    throw Failure("Expected a generic call or member boundary.");
                }
                return arguments;
            });
        }

        int Precedence()
        {
            if (secondClose)
            {
                return 0;
            }
            return Text() switch
            {
                "||" => 1,
                "&&" => 2,
                "==" or "!=" => 3,
                "<" or ">" or "<=" or ">=" => 4,
                "|" => 5,
                "^" => 6,
                "&" => 7,
                "<<" or ">>" => 8,
                "+" or "-" or "!+" or "!-" => 9,
                "*" or "/" or "%" or "!*" => 10,
                _ => 0
            };
        }

        SyntaxNode Expression(int minimum = 1, bool header = false)
        {
            Enter();
            try
            {
                SyntaxNode left = Prefix(header);
                while (Precedence() >= minimum)
                {
                    int precedence = Precedence();
                    int operation = Take();
                    // Raising the right threshold makes equal-precedence
                    // operators left associative, without a function per level.
                    SyntaxNode right = Expression(precedence + 1, header);
                    left = Node(SyntaxKind.Binary, operation, left, right);
                }
                return left;
            }
            finally
            {
                recursion -= 1;
            }
        }

        SyntaxNode Prefix(bool header)
        {
            if (At("-") || At("!-") || At("!") || At("ref"))
            {
                Enter();
                try
                {
                    int operation = Take();
                    return Node(SyntaxKind.Unary, operation, Prefix(header));
                }
                finally
                {
                    recursion -= 1;
                }
            }
            SyntaxNode result = Primary(header);
            while (true)
            {
                SyntaxNode? generic = TryGeneric();
                if (generic is not null)
                {
                    result = Node(SyntaxKind.Generic, -1, result, generic);
                }
                else if (At("("))
                {
                    Take();
                    SyntaxNode call = Node(SyntaxKind.Call, -1, result);
                    if (!At(")"))
                    {
                        do
                        {
                            call.AddChild(Expression());
                            if (!At(","))
                            {
                                break;
                            }
                            Take();
                        } while (!At(")"));
                    }
                    Expect(")");
                    result = call;
                }
                else if (At("."))
                {
                    Take();
                    int member = Kind("tuple-index") ? Take() : Name();
                    result = Node(SyntaxKind.Member, member, result);
                }
                else if (At("!") || At("?"))
                {
                    result = Node(SyntaxKind.Postfix, Take(), result);
                }
                else if (At("as") || At("!as"))
                {
                    int operation = Take();
                    result = Node(SyntaxKind.Cast, operation, result, Type(false));
                }
                else if (At("["))
                {
                    Take();
                    SyntaxNode? low = null;
                    if (!At(":"))
                    {
                        low = Expression();
                    }
                    if (At(":"))
                    {
                        Take();
                        SyntaxNode slice = Node(SyntaxKind.Slice, -1, result);
                        SyntaxNode lower = Node(SyntaxKind.LowerBound);
                        if (low is not null)
                        {
                            lower.AddChild(low);
                        }
                        slice.AddChild(lower);
                        SyntaxNode upper = Node(SyntaxKind.UpperBound);
                        if (!At("]"))
                        {
                            upper.AddChild(Expression());
                        }
                        slice.AddChild(upper);
                        result = slice;
                    }
                    else
                    {
                        result = Node(SyntaxKind.Index, -1, result, low!);
                    }
                    Expect("]");
                }
                else if (At("is"))
                {
                    Take();
                    result = Node(SyntaxKind.VariantTest, -1, result, QualifiedName());
                }
                else
                {
                    return result;
                }
            }
        }

        SyntaxNode QualifiedName()
        {
            SyntaxNode name = Node(SyntaxKind.Name, Name());
            // The repeated PEG unit is the whole "." Ident sequence.
            // Leave a tuple selector (or incomplete suffix) to the caller.
            while (At(".") && Tokens[cursor + 1].Kind == "identifier")
            {
                Take();
                name = Node(SyntaxKind.Member, Name(), name);
            }
            return name;
        }

        // Caller consumed the opener. Comma lists here allow a trailing comma;
        // angle lists deliberately use a different helper with no trailing comma.
        void ExpressionList(SyntaxNode parent, string close)
        {
            if (!At(close))
            {
                do
                {
                    parent.AddChild(Expression());
                    if (!At(","))
                    {
                        break;
                    }
                    Take();
                } while (!At(close));
            }
            Expect(close);
        }

        bool MemberSeparator()
        {
            if (At(","))
            {
                Take();
                SkipSeparators();
                return true;
            }
            if (Separator())
            {
                SkipSeparators();
                return true;
            }
            return false;
        }

        SyntaxNode Closure()
        {
            Expect("(");
            SyntaxNode closure = Node(SyntaxKind.Closure);
            SyntaxNode parameters = Node(SyntaxKind.Parameters);
            closure.AddChild(parameters);
            if (!At(")"))
            {
                do
                {
                    SyntaxNode parameter = Node(SyntaxKind.Parameter, Name());
                    Expect(":");
                    parameter.AddChild(Type());
                    parameters.AddChild(parameter);
                    if (!At(","))
                    {
                        break;
                    }
                    Take();
                } while (!At(")"));
            }
            Expect(")");
            Expect("->");
            closure.AddChild(Node(SyntaxKind.ResultType, -1, Type()));
            closure.AddChild(Block());
            return closure;
        }

        SyntaxNode Record()
        {
            // NamedType deliberately excludes optional and union suffixes.
            SyntaxNode type = NamedType();
            Expect("{");
            SyntaxNode record = Node(SyntaxKind.Record, -1, type);
            SkipSeparators();
            if (!At("}"))
            {
                do
                {
                    int field = Name();
                    Expect(":");
                    record.AddChild(Node(SyntaxKind.FieldValue, field, Expression()));
                    if (!MemberSeparator())
                    {
                        break;
                    }
                } while (!At("}"));
            }
            Expect("}");
            return record;
        }

        SyntaxNode Pattern()
        {
            Enter();
            try
            {
                if (Kind("discard"))
                {
                    return Node(SyntaxKind.Wildcard, Take());
                }
                if (At("-"))
                {
                    int minus = Take();
                    if (!Kind("integer"))
                    {
                        throw Failure("Expected an integer pattern.");
                    }
                    return Node(SyntaxKind.PatternNegative, minus, Node(SyntaxKind.Integer, Take()));
                }
                if (Kind("integer") || Kind("byte") || Kind("string")
                    || At("true") || At("false") || At("null"))
                {
                    // PatternLiteral excludes raw triples even though the lexer
                    // represents both raw and escaped strings with kind string.
                    if (Kind("string") && Text().StartsWith("\"\"\"", StringComparison.Ordinal))
                    {
                        throw Failure("Expected a pattern.");
                    }
                    return Node(SyntaxKind.PatternLiteral, Take());
                }
                if (At("("))
                {
                    Take();
                    SyntaxNode tuple = Node(SyntaxKind.PatternTuple, -1, Pattern());
                    Expect(",");
                    if (!At(")"))
                    {
                        tuple.AddChild(Pattern());
                    }
                    while (At(","))
                    {
                        Take();
                        if (At(")"))
                        {
                            break;
                        }
                        tuple.AddChild(Pattern());
                    }
                    Expect(")");
                    return tuple;
                }
                SyntaxNode name = Node(SyntaxKind.PatternName, -1, QualifiedName());
                if (At("("))
                {
                    Take();
                    SyntaxNode arguments = Node(SyntaxKind.PatternArguments);
                    name.AddChild(arguments);
                    if (!At(")"))
                    {
                        do
                        {
                            arguments.AddChild(Pattern());
                            if (!At(","))
                            {
                                break;
                            }
                            Take();
                        } while (!At(")"));
                    }
                    Expect(")");
                }
                return name;
            }
            finally
            {
                recursion -= 1;
            }
        }

        SyntaxNode If()
        {
            Enter();
            try
            {
                Expect("if");
                SyntaxNode condition;
                if (At("let"))
                {
                    Take();
                    SyntaxNode pattern = Pattern();
                    Expect("=");
                    condition = Node(SyntaxKind.ConditionBinding, -1, pattern, Expression(1, true));
                }
                else
                {
                    condition = Expression(1, true);
                }
                SyntaxNode result = Node(SyntaxKind.If, -1, condition, Block());
                // Only consume intervening newlines if else actually follows.
                // Otherwise the enclosing statement list still needs its separator.
                int saved = cursor;
                while (Kind("newline"))
                {
                    Take();
                }
                if (At("else"))
                {
                    Take();
                    result.AddChild(At("if") ? If() : Block());
                }
                else
                {
                    cursor = saved;
                }
                return result;
            }
            finally
            {
                recursion -= 1;
            }
        }

        SyntaxNode MatchType()
        {
            int keyword = Take();
            SyntaxNode parameters = Node(SyntaxKind.TypeParameters);
            if (At("<"))
            {
                Take();
                do
                {
                    parameters.AddChild(Node(SyntaxKind.TypeParameter, Name()));
                    if (!At(","))
                    {
                        break;
                    }
                    Take();
                } while (true);
                CloseType();
            }
            // Keep a fixed child layout even when the declaration list is empty:
            // parameters, unevaluated subject, then source-ordered arms.
            SyntaxNode result = Node(SyntaxKind.MatchType, keyword,
                parameters, Expression(1, true));
            Expect("{");
            SkipSeparators();
            while (!At("}"))
            {
                SyntaxNode pattern = At("_")
                    ? Node(SyntaxKind.Wildcard, Take()) : Type();
                Expect("=>");
                result.AddChild(Node(SyntaxKind.MatchTypeArm, -1, pattern, Block()));
                if (!At("}") && !Separator())
                {
                    throw Failure("Expected a matchtype-arm separator.");
                }
                SkipSeparators();
            }
            Expect("}");
            return result;
        }

        SyntaxNode Match()
        {
            Expect("match");
            SyntaxNode match = Node(SyntaxKind.Match, -1, Expression(1, true));
            Expect("{");
            SkipSeparators();
            while (!At("}"))
            {
                SyntaxNode alternatives = Node(SyntaxKind.Patterns, -1, Pattern());
                while (At("|"))
                {
                    Take();
                    alternatives.AddChild(Pattern());
                }
                SyntaxNode arm = Node(SyntaxKind.MatchArm, -1, alternatives);
                if (At("if"))
                {
                    Take();
                    arm.AddChild(Node(SyntaxKind.Guard, -1, Expression(1, true)));
                }
                Expect("=>");
                arm.AddChild(Block());
                match.AddChild(arm);
                if (!At("}") && !Separator())
                {
                    throw Failure("Expected a match-arm separator.");
                }
                SkipSeparators();
            }
            Expect("}");
            return match;
        }

        SyntaxNode Primary(bool header)
        {
            if (At("if"))
            {
                return If();
            }
            if (At("matchtype"))
            {
                return MatchType();
            }
            if (At("match"))
            {
                return Match();
            }
            if (At("("))
            {
                SyntaxNode? closure = Attempt(Closure);
                if (closure is not null)
                {
                    return closure;
                }
                Take();
                if (At(")"))
                {
                    Take();
                    return Node(SyntaxKind.Unit);
                }
                SyntaxNode first = Expression();
                if (At(","))
                {
                    Take();
                    SyntaxNode tuple = Node(SyntaxKind.Tuple, -1, first);
                    if (!At(")"))
                    {
                        tuple.AddChild(Expression());
                    }
                    while (At(","))
                    {
                        Take();
                        if (At(")"))
                        {
                            break;
                        }
                        tuple.AddChild(Expression());
                    }
                    Expect(")");
                    return tuple;
                }
                Expect(")");
                return Node(SyntaxKind.Group, -1, first);
            }
            if (At("["))
            {
                Take();
                SyntaxNode array = Node(SyntaxKind.Array);
                ExpressionList(array, "]");
                return array;
            }
            if (!header && At("{"))
            {
                Take();
                SyntaxNode dictionary = Node(SyntaxKind.Dictionary);
                SkipSeparators();
                if (!At("}"))
                {
                    do
                    {
                        SyntaxNode key = Expression();
                        Expect(":");
                        dictionary.AddChild(Node(SyntaxKind.Entry, -1, key, Expression()));
                        if (!MemberSeparator())
                        {
                            break;
                        }
                    } while (!At("}"));
                }
                Expect("}");
                return dictionary;
            }
            if (!header && Kind("identifier"))
            {
                SyntaxNode? record = Attempt(Record);
                if (record is not null)
                {
                    return record;
                }
            }
            if (Kind("fstring-start"))
            {
                Take();
                SyntaxNode text = Node(SyntaxKind.InterpolatedString);
                while (!Kind("fstring-end"))
                {
                    if (Kind("string-text"))
                    {
                        text.AddChild(Node(SyntaxKind.StringText, Take()));
                    }
                    else
                    {
                        Expect("{");
                        text.AddChild(Node(SyntaxKind.Interpolation, -1, Expression()));
                        Expect("}");
                    }
                }
                Take();
                return text;
            }
            if (Kind("integer") || Kind("float") || Kind("string")
                || Kind("byte") || Kind("symbol"))
            {
                SyntaxKind kind = Current().Kind switch
                {
                    "integer" => SyntaxKind.Integer,
                    "float" => SyntaxKind.Float,
                    "string" => SyntaxKind.String,
                    "byte" => SyntaxKind.Byte,
                    "symbol" => SyntaxKind.Symbol,
                    _ => throw new InvalidOperationException("Not a literal token.")
                };
                return Node(kind, Take());
            }
            if (At("true") || At("false") || At("null"))
            {
                return Node(SyntaxKind.Literal, Take());
            }
            if (Kind("identifier") || At("self"))
            {
                return Node(SyntaxKind.Name, Take());
            }
            throw Failure("Expected an expression.");
        }

        SyntaxNode Binding(bool global)
        {
            SyntaxNode binding = Node(global ? SyntaxKind.Global : SyntaxKind.Binding, Take());
            binding.AddChild(global ? Node(SyntaxKind.Name, Name()) : Pattern());
            bool typed = At(":");
            if (typed)
            {
                Take();
                binding.AddChild(Node(SyntaxKind.Annotation, -1, Type()));
            }
            if (At("="))
            {
                Take();
                binding.AddChild(Expression());
            }
            else if (global || !typed)
            {
                throw Failure("Expected '='.");
            }
            if (!global && At("else"))
            {
                Take();
                binding.AddChild(Node(SyntaxKind.Else, -1, Block()));
            }
            return binding;
        }

        SyntaxNode Statement()
        {
            if (At("prepare") && cursor + 1 < Tokens.Count && TokenText(cursor + 1) == "{")
            {
                Take();
                return Node(SyntaxKind.Prepare, -1, Block());
            }
            if (At("let") || At("var"))
            {
                return Binding(false);
            }
            if (At("return"))
            {
                Take();
                SyntaxNode statement = Node(SyntaxKind.Return);
                if (!Separator() && !At("}") && !Kind("end"))
                {
                    statement.AddChild(Expression());
                }
                return statement;
            }
            if (At("yield"))
            {
                Take();
                return Node(SyntaxKind.Yield, -1, Expression());
            }
            if (At("break") || At("continue"))
            {
                SyntaxKind kind = At("break") ? SyntaxKind.Break : SyntaxKind.Continue;
                Take();
                return Node(kind);
            }
            if (At("while"))
            {
                Take();
                return Node(SyntaxKind.While, -1, Expression(1, true), Block());
            }
            if (At("for"))
            {
                Take();
                SyntaxNode loop = Node(SyntaxKind.For, -1, Pattern());
                if (At(","))
                {
                    Take();
                    loop.AddChild(Node(SyntaxKind.IterationIndex, Name()));
                }
                Expect("in");
                loop.AddChild(Node(SyntaxKind.Iterable, -1, Expression(1, true)));
                loop.AddChild(Block());
                return loop;
            }
            if (At("{"))
            {
                SyntaxNode? block = Attempt(Block);
                if (block is not null)
                {
                    return block;
                }
            }
            SyntaxNode expression = Expression();
            if (At("=") || At("+=") || At("-=") || At("*=") || At("/=")
                || At("!+=") || At("!-=") || At("!*="))
            {
                int operation = Take();
                return Node(SyntaxKind.Assignment, operation, expression, Expression());
            }
            return Node(SyntaxKind.ExpressionStatement, -1, expression);
        }

        SyntaxNode Block()
        {
            Enter();
            try
            {
                Expect("{");
                SyntaxNode block = Node(SyntaxKind.Block);
                SkipSeparators();
                while (!At("}"))
                {
                    if (Kind("end"))
                    {
                        throw Failure("Expected '}'.");
                    }
                    SyntaxNode statement = Statement();
                    block.AddChild(statement);
                    if (!At("}") && !Separator())
                    {
                        throw Failure("Expected a statement separator.");
                    }
                    // Newlines do not discard a value; a semicolon anywhere in
                    // this separator run does. Preserve the first explicit one.
                    int semicolon = -1;
                    while (Separator())
                    {
                        if (At(";") && semicolon < 0)
                        {
                            semicolon = cursor;
                        }
                        Take();
                    }
                    if (semicolon >= 0)
                    {
                        block.AddChild(Node(SyntaxKind.DiscardResult, semicolon));
                    }
                }
                Expect("}");
                return block;
            }
            finally
            {
                recursion -= 1;
            }
        }

        SyntaxNode TypeParameters(bool participants = false)
        {
            Expect("<");
            SyntaxNode parameters = Node(SyntaxKind.TypeParameters);
            do
            {
                // Role words are contextual only in interface parameter lists.
                int role = participants && (At("parent") || At("child")) ? Take() : -1;
                SyntaxNode parameter = Node(SyntaxKind.TypeParameter, Name());
                if (role >= 0)
                {
                    parameter.AddChild(Node(SyntaxKind.Modifier, role));
                }
                if (At(":"))
                {
                    Take();
                    parameter.AddChild(QualifiedName());
                }
                parameters.AddChild(parameter);
                if (!At(","))
                {
                    break;
                }
                Take();
            } while (true);
            CloseType();
            return parameters;
        }

        SyntaxNode Capability()
        {
            SyntaxNode capability = Node(SyntaxKind.Capability, -1, QualifiedName());
            if (At("<"))
            {
                capability.AddChild(TypeArguments(true));
            }
            return capability;
        }

        SyntaxNode Requirement()
        {
            SyntaxNode? simple = Attempt(() =>
            {
                int name = Name();
                Expect(":");
                return Node(SyntaxKind.Requirement, name, QualifiedName());
            });
            return simple ?? Capability();
        }

        SyntaxNode Function(bool relationship = false)
        {
            int modifier = -1;
            if (At("safe") || At("final")
                || (relationship && (At("add") || At("remove"))))
            {
                modifier = Take();
            }
            Expect("func");
            SyntaxNode function = Node(SyntaxKind.Function, -1, QualifiedName());
            if (modifier >= 0)
            {
                function.AddChild(Node(SyntaxKind.Modifier, modifier));
            }
            if (At("<"))
            {
                function.AddChild(TypeParameters());
            }
            Expect("(");
            SyntaxNode parameters = Node(SyntaxKind.Parameters);
            function.AddChild(parameters);
            if (!At(")"))
            {
                do
                {
                    SyntaxNode parameter;
                    if (At("self"))
                    {
                        parameter = Node(SyntaxKind.Parameter, Take());
                        // A receiver may explicitly request reference semantics.
                        // Signature checking determines which annotations are legal.
                        if (At(":"))
                        {
                            Take();
                            parameter.AddChild(Type());
                        }
                    }
                    else
                    {
                        parameter = Node(SyntaxKind.Parameter, Name());
                        Expect(":");
                        parameter.AddChild(Type());
                    }
                    parameters.AddChild(parameter);
                    if (!At(","))
                    {
                        break;
                    }
                    Take();
                } while (!At(")"));
            }
            Expect(")");
            if (At("->"))
            {
                Take();
                function.AddChild(Node(SyntaxKind.ResultType, -1, Type()));
            }
            if (At("where"))
            {
                Take();
                SyntaxNode requirements = Node(SyntaxKind.Where, -1, Requirement());
                while (At(","))
                {
                    Take();
                    requirements.AddChild(Requirement());
                }
                function.AddChild(requirements);
            }
            if (At("{"))
            {
                function.AddChild(Block());
            }
            return function;
        }

        SyntaxNode Field()
        {
            int visibility = At("pub") ? Take() : -1;
            SyntaxNode field = Node(SyntaxKind.Field, Name());
            Expect(":");
            field.AddChild(Type());
            if (At("="))
            {
                Take();
                field.AddChild(Expression());
            }
            if (visibility >= 0)
            {
                field.AddChild(Node(SyntaxKind.Visibility, visibility));
            }
            return field;
        }

        SyntaxNode NamedType()
        {
            SyntaxNode type = Node(SyntaxKind.TypeName, Name());
            // The repeated PEG unit is the whole "." Ident sequence.
            // Leave a tuple selector (or incomplete suffix) to the caller.
            while (At(".") && Tokens[cursor + 1].Kind == "identifier")
            {
                Take();
                type = Node(SyntaxKind.TypeMember, Name(), type);
            }
            if (At("<"))
            {
                // Optional TypeArgs must roll back as a whole. In a cast,
                // an unmatched angle may instead begin a comparison.
                SyntaxNode? arguments = Attempt(() => TypeArguments(false));
                if (arguments is not null)
                {
                    type = Node(SyntaxKind.TypeApplication, -1, type, arguments);
                }
            }
            return type;
        }

        SyntaxNode Endpoint()
        {
            SyntaxNode endpoint = Node(SyntaxKind.Endpoint, -1, NamedType());
            if (At(":"))
            {
                Take();
                endpoint.AddChild(Node(SyntaxKind.Role, Name()));
            }
            return endpoint;
        }

        SyntaxNode Relation(bool abstractRelation)
        {
            Expect("relation");
            SyntaxNode relation = Node(abstractRelation ? SyntaxKind.AbstractRelation : SyntaxKind.Relation,
                -1, QualifiedName(), Endpoint());
            if (At("owns") || At("refs"))
            {
                relation.AddChild(Node(SyntaxKind.RelationKind, Take()));
            }
            else if (!abstractRelation)
            {
                throw Failure("Expected 'owns' or 'refs'.");
            }
            if (At("["))
            {
                Take();
                relation.AddChild(Node(SyntaxKind.ManyEndpoint, -1, Endpoint()));
                Expect("]");
            }
            else
            {
                relation.AddChild(Endpoint());
            }
            return relation;
        }

        SyntaxNode DestructorFragment()
        {
            Expect("partial");
            if (!At("owns") && !At("refs"))
            {
                throw Failure("Expected destructor policy 'owns' or 'refs'.");
            }
            int policy = Take();
            int name = Expect("destroy");
            Expect("(");
            int receiver = Expect("self");
            Expect(")");
            // Preserve a distinct declaration kind, but use the ordinary callable
            // signature shape so checking owns a real self parameter and scope.
            return Node(SyntaxKind.DestructorFragment, name,
                Node(SyntaxKind.Name, name),
                Node(SyntaxKind.RelationKind, policy),
                Node(SyntaxKind.Parameters, -1, Node(SyntaxKind.Parameter, receiver)),
                Block());
        }

        SyntaxNode Aggregate(bool isClass, bool participant = false)
        {
            int value = At("value") ? Take() : -1;
            Expect(isClass ? "class" : "struct");
            SyntaxNode aggregate = Node(isClass ? SyntaxKind.Class : SyntaxKind.Struct, Name());
            if (value >= 0)
            {
                aggregate.AddChild(Node(SyntaxKind.Modifier, value));
            }
            // A participant names an existing interface parameter. It does not
            // introduce a second generic class declaration inside that contract.
            if (!participant && At("<"))
            {
                aggregate.AddChild(TypeParameters());
            }
            Expect("{");
            SkipSeparators();
            while (!At("}"))
            {
                // Structs and classes share member syntax, not value/lifetime
                // semantics. Preserve the same function tree in either body.
                SyntaxNode? function = Attempt(() =>
                {
                    int visibility = At("pub") ? Take() : -1;
                    SyntaxNode member = participant && At("partial")
                        ? DestructorFragment() : Function(relationship: participant);
                    if (visibility >= 0)
                    {
                        member.AddChild(Node(SyntaxKind.Visibility, visibility));
                    }
                    return member;
                });
                aggregate.AddChild(function ?? Field());
                if (!MemberSeparator())
                {
                    break;
                }
            }
            Expect("}");
            return aggregate;
        }

        SyntaxNode Enum()
        {
            Expect("enum");
            SyntaxNode enumeration = Node(SyntaxKind.Enum, Name());
            if (At("<"))
            {
                enumeration.AddChild(TypeParameters());
            }
            Expect("{");
            SkipSeparators();
            while (!At("}"))
            {
                SyntaxNode variant = Node(SyntaxKind.Variant, Name());
                if (At("("))
                {
                    Take();
                    SyntaxNode fields = Node(SyntaxKind.VariantFields);
                    variant.AddChild(fields);
                    if (!At(")"))
                    {
                        do
                        {
                            int name = Name();
                            Expect(":");
                            fields.AddChild(Node(SyntaxKind.VariantField, name, Type()));
                            if (!At(","))
                            {
                                break;
                            }
                            Take();
                        } while (!At(")"));
                    }
                    Expect(")");
                }
                enumeration.AddChild(variant);
                // Adjacent enum alternatives are intentionally allowed by PEG.
                MemberSeparator();
            }
            Expect("}");
            return enumeration;
        }

        SyntaxNode Interface()
        {
            Expect("interface");
            SyntaxNode contract = Node(SyntaxKind.Interface, Name());
            if (At("<"))
            {
                contract.AddChild(TypeParameters(participants: true));
            }
            if (At("extends"))
            {
                Take();
                contract.AddChild(Node(SyntaxKind.Extends, -1, Capability()));
            }
            Expect("{");
            SkipSeparators();
            while (!At("}"))
            {
                SyntaxNode? member = Attempt(() =>
                {
                    int visibility = At("pub") ? Take() : -1;
                    SyntaxNode function = Function();
                    if (visibility >= 0)
                    {
                        function.AddChild(Node(SyntaxKind.Visibility, visibility));
                    }
                    return function;
                });
                if (member is null)
                {
                    if (At("class"))
                    {
                        member = Aggregate(true, participant: true);
                    }
                    else if (At("field"))
                    {
                        Take();
                        int owner = Name();
                        Expect(".");
                        int field = Name();
                        Expect(":");
                        member = Node(SyntaxKind.InterfaceField, field, Node(SyntaxKind.Name, owner), Type());
                    }
                    else if (At("relation"))
                    {
                        member = Relation(true);
                    }
                    else
                    {
                        Expect("destructor");
                        if (!At("owns") && !At("refs"))
                        {
                            throw Failure("Expected 'owns' or 'refs'.");
                        }
                        int ownership = Take();
                        int name = Name();
                        member = Node(SyntaxKind.CleanupTemplate, name,
                            Node(SyntaxKind.RelationKind, ownership), Block());
                    }
                }
                contract.AddChild(member);
                if (!At("}") && !Separator())
                {
                    throw Failure("Expected an interface-member separator.");
                }
                SkipSeparators();
            }
            Expect("}");
            return contract;
        }

        SyntaxNode MemberPath()
        {
            SyntaxNode path = Node(SyntaxKind.Name, Name());
            Expect(".");
            path = Node(SyntaxKind.Member, Name(), path);
            while (At("."))
            {
                Take();
                path = Node(SyntaxKind.Member, Name(), path);
            }
            return path;
        }

        SyntaxNode RelationRoles()
        {
            SyntaxNode parent = Endpoint();
            Expect("/");
            return Node(SyntaxKind.RelationRoles, -1, parent, Endpoint());
        }

        SyntaxNode Adapter()
        {
            Expect("(");
            SyntaxNode adapter = Node(SyntaxKind.Adapter);
            SyntaxNode parameters = Node(SyntaxKind.Parameters);
            adapter.AddChild(parameters);
            if (!At(")"))
            {
                do
                {
                    SyntaxNode parameter;
                    if (At("self"))
                    {
                        parameter = Node(SyntaxKind.Parameter, Take());
                    }
                    else
                    {
                        parameter = Node(SyntaxKind.Parameter, Name());
                        if (At(":"))
                        {
                            Take();
                            parameter.AddChild(Type());
                        }
                    }
                    parameters.AddChild(parameter);
                    if (!At(","))
                    {
                        break;
                    }
                    Take();
                } while (!At(")"));
            }
            Expect(")");
            Expect("->");
            adapter.AddChild(Node(SyntaxKind.ResultType, -1, Type()));
            adapter.AddChild(Block());
            return adapter;
        }

        SyntaxNode Impl()
        {
            Expect("impl");
            SyntaxNode implementation = Node(SyntaxKind.Impl);
            if (At("<"))
            {
                implementation.AddChild(TypeParameters());
            }
            implementation.AddChild(QualifiedName());
            implementation.AddChild(TypeArguments(true));
            if (At("owns") || At("refs"))
            {
                implementation.AddChild(Node(SyntaxKind.RelationKind, Take()));
            }
            Expect("{");
            SkipSeparators();
            while (!At("}"))
            {
                SyntaxNode? member = Attempt(() =>
                {
                    SyntaxNode left = RelationRoles();
                    Expect("=");
                    return Node(SyntaxKind.RelationBinding, -1, left, RelationRoles());
                });
                if (member is null)
                {
                    SyntaxNode left = MemberPath();
                    if (At("<->"))
                    {
                        Take();
                        member = Node(SyntaxKind.FieldBinding, -1, left, MemberPath());
                    }
                    else
                    {
                        Expect("=");
                        SyntaxNode right = At("(") ? Adapter() : MemberPath();
                        member = Node(SyntaxKind.MethodBinding, -1, left, right);
                    }
                }
                implementation.AddChild(member);
                if (!At("}") && !Separator())
                {
                    throw Failure("Expected an implementation-member separator.");
                }
                SkipSeparators();
            }
            Expect("}");
            return implementation;
        }

        SyntaxNode Declaration()
        {
            if (At("import"))
            {
                Take();
                SyntaxNode import = Node(SyntaxKind.Import, Name());
                if (At("from"))
                {
                    Take();
                    if (!Kind("string") || Text().StartsWith("\"\"\"", StringComparison.Ordinal))
                    {
                        throw Failure("Expected an ordinary string.");
                    }
                    import.AddChild(Node(SyntaxKind.String, Take()));
                }
                return import;
            }
            if (At("impl"))
            {
                return Impl();
            }
            if (At("relation"))
            {
                return Relation(false);
            }
            int visibility = At("pub") ? Take() : -1;
            SyntaxNode item;
            if (At("type"))
            {
                Take();
                int name = Name();
                Expect("=");
                item = Node(SyntaxKind.TypeAlias, name, Type());
            }
            else if (At("struct"))
            {
                item = Aggregate(false);
            }
            else if (At("class") || At("value"))
            {
                item = Aggregate(true);
            }
            else if (At("enum"))
            {
                item = Enum();
            }
            else if (At("interface"))
            {
                item = Interface();
            }
            else if (At("let") || At("var"))
            {
                item = Binding(true);
            }
            else
            {
                item = Function();
            }
            if (visibility >= 0)
            {
                item.AddChild(Node(SyntaxKind.Visibility, visibility));
            }
            return item;
        }

        try
        {
            SyntaxNode root = Node(expressionOnly ? SyntaxKind.Expression : SyntaxKind.File);
            SkipSeparators();
            if (expressionOnly)
            {
                root.AddChild(Expression());
                SkipSeparators();
            }
            else
            {
                while (!Kind("end"))
                {
                    root.AddChild(Declaration());
                    if (!Kind("end") && !Separator())
                    {
                        throw Failure("Expected a declaration separator.");
                    }
                    SkipSeparators();
                }
            }
            if (!Kind("end"))
            {
                throw Failure("Expected end of input.");
            }
            return null;
        }
        catch (SyntaxFailure failure)
        {
            ClearSyntax(0);
            return failure.Diagnostic;
        }
    }
}
