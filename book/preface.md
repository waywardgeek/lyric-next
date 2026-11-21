# Preface

Agentic codebooks create an opportunity for vastly accelerated improvement of our software systems. Instead of treating a codebase as the only durable result of development, we can preserve a reproducible description of how to build it: the architecture, the algorithms, the reasons for important choices, and the checks that tell us whether the result is correct.

To add a significant new capability, add a chapter. To improve an existing design, edit the chapters that teach it. Then re-execute the book. We can build the next implementation from a better understanding instead of carrying every workaround and accidental dependency forward indefinitely.

When we discover a mistake, we repair both the software and the instructions that produced it. The correction becomes part of how the next implementation is constructed, rather than a patch whose reason is eventually forgotten. Engineering judgment becomes something we can preserve, improve, and execute again.

## A book that builds a compiler

Hand the completed first edition of this book to an AI coding agent such as Claude Code, Codex, or [Ensemble](https://github.com/waywardgeek/ensemble), and the agent will give you a new working Lyric compiler written in C#. The exercises are the construction steps. Together, their solutions become the compiler.

We will develop this first edition in C# while building and checking the work it teaches. C# is a good fit for my preferred architecture: Common declares the vocabulary, persistent state, relationships, and callable operations; functional modules implement that vocabulary; the application connects them. Partial types let us express that separation directly without wrappers or duplicated state.

I want an agent to reproduce the design, not merely imitate the output. The book must explain how the compiler is organized, why its objects have the relationships they do, what happens when an operation fails, and how to verify the result.

## A language shaped by experience and an AI collaborator

Lyric combines my extensive experience building languages and software systems with Cadence's preferences and deep insights as an AI engineering collaborator. We are designing a language that we both want to work in. I bring decades of experience with explicit relationships, generated code, memory layout, and systems architecture. Cadence brings a perspective on what makes code and specifications consistent, understandable, and effective for an LLM to generate and review.

That collaboration matters. We do not have to assume that the best language for an AI is simply whichever existing language has the most training data. We can design the language around clear rules and useful abstractions, learn from the model's difficulties, and improve the language and its teaching together.

The prior version of Lyric already demonstrated the value of this approach to language design. In our self-compilation tests on a MacBook Air M2, switching from Array-of-Structs to Struct-of-Arrays memory layout made the compiler **10% faster and reduced memory use by 14%**, with no changes to its Lyric source.

The first self-hosting compiler was also substantially smaller than the Go compiler that bootstrapped it: **26,813 lines instead of 33,739**, a **20.5% reduction**, with **9.9% fewer source bytes**. Those [original Lyric results](https://github.com/waywardgeek/lyric#readme) give us a concrete foundation to build on. Relationships and generated cleanup can remove work from both the programmer and the machine.

## The second edition will self-host

After we finish building and validating Edition 1, we will hand it to a coding agent again. This time, we will ask for the exercise solutions to be written in Lyric rather than C#.

I expect the result to be a mostly autonomous conversion of the entire compiler codebase from C# to Lyric. The C# compiler will provide the bootstrap needed to compile the new implementation. The resulting Lyric compiler will then compile its own source.

During that same execution, we will create Edition 2 of the book. Its explanations, examples, algorithms, and checks will describe the Lyric implementation being developed. We will produce the self-hosted compiler and its reproducible construction specification together.

The opportunity is larger than translating syntax. We can ask an agent to preserve the architecture and behavior while expressing them naturally in another language. The book carries the engineering knowledge that makes that reconstruction possible.

## Improving the agent, the language, and the model together

Ensemble is itself developed through an agentic codebook. With both Ensemble and Lyric expressed this way, we can connect improvements to the agent system, the language, and the compiler with LLM training.

This book has the potential to revolutionize how AI builds code, evolving both the language and the LLM together. We can make Lyric a better language for the model while training the model to use Lyric more effectively.

The construction work supplies more than finished programs. It supplies attempted solutions, visible engineering explanations, compiler diagnostics, failed tests, corrections, and verified outcomes. We can use those experiences as training material, evaluate what the model learns on new tasks, and feed the useful discoveries back into the books. Failures and human interventions belong in that record too: they teach lessons that polished final code cannot.

A better language makes the model's job easier. A better-trained model becomes more effective at improving the compiler and the systems written in Lyric. Better agent tooling helps it conduct and evaluate those experiments. The books carry that growing understanding into the next construction.

We are building toward superintelligence: each loop faster, more powerful, and better suited to that evolving intelligence.

— Bill Cox
