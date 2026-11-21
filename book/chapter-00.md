# Chapter 0 — The Compiler We Will Build

**Working draft: TL;DR and exercises only.** This establishes the proposed construction contract for review. The teaching body, practical walkthrough, and chapter-trial evidence will follow development; they have not been completed. This chapter does not claim an implemented compiler.

## TL;DR

We will construct a Lyric compiler whose implementation can be reproduced and understood from this book. The [Preface](preface.md) explains Bill's motivation, the two-edition plan, and the proposed connection to model training. This chapter establishes the technical construction contract.

This is a development-driven codebook. We specify a small increment, build and test it, investigate what fails, and incorporate the demonstrated design into the chapter. We do not build a finished compiler and ask a later author to reconstruct its reasoning. Nor do we treat an early outline as more authoritative than what development teaches us.

### Two specifications, two jobs

The [language specification](../lyric-language-spec.md) defines accepted programs, their meaning, lifetime obligations, and failures. The [PEG grammar](../lyric-grammar.peg) and [grammar review](../grammar-review.md) record syntax and its remaining decisions. An example in a draft is not proof that a parser accepts it.

This book defines how to construct the compiler: its authoritative data, relationships, algorithms, implementation steps, and verification. If implementing a chapter reveals an ambiguity in the language, resolve the requirement with the human and update its authoritative home. Do not let a convenient parser or backend accident silently become language semantics.

Historical designs under background and reviews explain where choices came from. They do not override the current specification. The original compiler is useful evidence and a source of reusable ideas, not an oracle for features whose semantics we deliberately changed.

### Proposed starting target

The bootstrap implementation uses **C# 14 and .NET 10**, with **C as the output language**. The implementation initially runs single-threaded and supports a documented single-threaded Lyric subset. Later language features remain in the language design; they are not silently supported by the first executable.

Whole-program compilation, one supported development platform at first, and a small compiler-oriented library keep the initial boundary manageable. The exact platform/toolchain versions and launch commands must be established in the first executable chapter. This draft does not authorize dependency installation or claim those prerequisites have been verified.

Unsupported constructs must produce clear diagnostics and no purportedly successful executable. A chapter may implement only a subset, but it must say which subset and reject uses outside it. This is not permission to compile ordinary code without required safety checks and call the result safe.

.NET's garbage collector manages the bootstrap compiler's host memory. It does not determine Lyric's memory model. Generated Lyric programs must obey Lyric's declared lifetimes, reference counting, relationships, and checked failures without a tracing collector.

### Architecture is part of the assignment

Common owns the shared vocabulary: types, persistent state, parent/child relationships, and declarations of operations callable across functional modules. Common is a leaf in the application dependency graph; it does not import functional modules or implement their algorithms.

Functional modules supply implementations using that Common vocabulary. The top-level driver constructs and connects the system and sequences its work. A functional module must not depend on another module's implementation-only helpers or create copied state merely to make data easier to reach.

In the C# bootstrap, matching partial declarations and implementations compile together in **one assembly**. Common declares explicitly accessible partial methods and constructors; the responsible functional source supplies their bodies. Directories are source modules, not separate DLLs that can extend one another's partial types. Compiler diagnostics for missing implementations help, but C# does not automatically enforce our entire dependency discipline.

The compiler's object graph must demonstrate the rules it implements. Constructors establish required parent relationships through complete two-ended operations. Refused mutations leave both ends unchanged. Logical destruction detaches every parent, handles strong and weak children correctly, and completes any owned work before its lifetime ends. Garbage collection is not a substitute for these operations. Value-like types are identified explicitly rather than supplied with unnecessary holder backlinks.

We will define concrete compiler types when an increment earns them. Chapter 0 does not invent a full AST, service registry, universal pass framework, or extensibility system in advance.

### Compiler milestones and their evidence

The edition strategy is described in the Preface. Its implementation milestones require distinct evidence:

1. **Bootstrap:** the C# compiler accepts the required Lyric subset, rejects its invalid cases, and emits C whose execution matches the language contracts.
2. **Lyric implementation:** a compiler written in the supported Lyric subset is compiled by the bootstrap and passes the same relevant conformance suite.
3. **Self-hosting:** the Lyric compiler compiles its own source. Repeated stages are compared under controlled inputs and toolchain settings, with fixed-point criteria specified before claiming success.

A fixed point alone does not prove correctness: a compiler can reproduce its own bug. Positive execution tests, negative compilation tests, ownership/lifecycle checks, and human use remain necessary. Conversely, passing a few examples does not establish self-hosting. The book must distinguish each piece of evidence.

### How chapters earn acceptance

Each executable increment starts with requirements and exercises, then develops implementation and graders together. Graders accept an independently supplied implementation and live outside its solution tree. Their requirements are public even when their implementation or fixtures are private. Tests check real behavior; small working fakes may replace external boundaries, but not the implementation under test.

After the increment works, the chapter teaches its demonstrated construction, including important failures, reasons, and verification limits. The authoring process includes one code review, one text review, and a fresh student's construction trial, followed by human acceptance and a practical Take it for a Spin walkthrough. A passing student is not automatically rerun to chase cosmetic improvements.

For this orientation draft there is no implementation grader, student trial, or executable walkthrough yet. Do not manufacture one merely to label the chapter complete. The next executable chapter must supply verified commands, prerequisites, expected behavior, and cleanup steps.

## Exercises — establish the construction contract

These are orientation checks, not instructions to implement the compiler or change the language specification. They use the documents already in this repository. There are no API credentials, paid inference calls, installation steps, or external effects required by these exercises.

### 1. Identify authority and scope

Read the Preface, the language specification's Sections 1, 2, 17, 18, and 19, and the status section of the grammar review. Be able to explain:

- Which document decides whether a Lyric program is valid, and which document will teach the compiler algorithms.
- Why an old compiler accepting a construct does not override the new specification.
- How the technical milestones below support the edition plan in the Preface, without treating an expected outcome as an already demonstrated result.
- Which remaining initial-scope choices are proposals awaiting acceptance, rather than already tested implementation facts.
- Why an unresolved feature must be resolved or explicitly excluded from an increment, not implemented by guesswork.

Success means the agent and human agree on the next increment's supported boundary without turning the whole open-decision register into a prerequisite for starting.

### 2. Trace the architectural obligations

Explain where a shared type's fields, constructor declaration, constructor body, and cross-module operation belong in the C# implementation. Then explain how a child reaches its owning compilation context without a copied service bag or global search.

Distinguish a temporary algorithm reference from a persistent model relationship. For a persistent relationship, explain who validates attachment, who updates both ends, what refusal preserves, and who removes it during destruction. Do not invent the complete compiler object model to answer these questions.

Success means the proposed first executable slice can demonstrate these rules directly, and a functional module does not need another module's hidden implementation state to do its work.

### 3. Separate the claims we will test

For each statement, identify what would establish it and what would not:

- “This source is valid Lyric.”
- “This invalid source is rejected before executable output.”
- “This generated program obeys its lifetime rules.”
- “This compiler respects the Common/functional-module architecture.”
- “The Lyric compiler can compile itself.”

Include at least one case where successful execution is insufficient—for example, a use-after-destruction program that happens not to crash. Tests and a written safety argument have different roles; neither a document hash nor a self-compilation result proves all-program memory safety.

### Completion boundary for this draft

This draft is ready for discussion when its links resolve and its proposed scope, architecture, and exercises agree with the current language design. It is **not an accepted, execution-ready chapter** until Bill approves the construction contract and the chapter-writing process supplies the remaining teaching and applicable verification.

Do not infer permission to start the compiler, launch students/reviewers, install tools, or publish from this draft. The next step is to agree on the first useful executable increment, then draft that increment's TL;DR and exercises before implementation.
