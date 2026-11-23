# Chapter 9 construction plan — structured execution IR

Status: development in progress, not completed teaching or student instructions.

Keep the checked AST unchanged. Lower resolved declarations and facts to structured
IR, make ownership effects explicit, validate the concrete representation, and
translate it to C without reconstructing ownership in the emitter. Retain C-like
control flow instead of introducing basic blocks or a mandatory SSA pass.

The first working slice supports scalar functions: parameters, local storage,
value snapshots, arithmetic, calls, assignments, casts, conditional yields, while,
break/continue and return. Work stacks preserve deep-expression traversal without
host recursion. A flat JSON inspection format exposes actual nodes and relationships;
public Python graders check structure and interpret a bounded scalar subset.
See ../../graders/ch09/README.md for exact commands and inspection requirements.

Array/reference/class lowering now expresses existing ownership behavior, including
normal and transferring cleanup. Structure, operation and execution checks run before
publication. C emission now consumes that IR exclusively: runtime/layout/arithmetic
helpers remain, but syntax execution and emitter-owned cleanup reconstruction are
removed. Full IR-only cumulative and sanitizer verification passed; see
ch09-checks/ir-c-only-milestone.md for actual receipts and limits. No optimizer pass
or new source feature has been added. Remaining work includes completed teaching,
review, and independent reconstruction; bounded validation does not prove arbitrary
alias-sensitive ownership liveness.

Generics and relations are future elaboration work, not prerequisites for this
refactor. Concrete specialization identity must include its declaration and complete
binding, not just parameter types. Later unreachable-function pruning must follow
both ordinary calls and generated cleanup dependencies.

## Ownership-lowering construction constraints

Preserve the existing distinction between selecting storage and capturing an
address. A direct class-field assignment saves the receiver index before the
right-hand side, then resolves the field at the eventual store. Class allocation
inside the right-hand side may relocate SoA buffers. A reference instead captures
a fixed address at acquisition; it must never be silently re-resolved after an
invalidation. Indexed destinations similarly preserve the selected address before
the right-hand side. Represent these distinctions explicitly before enabling those
operations in lowering; one undifferentiated address operation is insufficient.

Cleanup acts on owning holders. Copies establish independent array ownership or
retain counted identity; transfers empty the source holder. A later cleanup of an
empty holder is harmless. Emit ownership actions in ordered regions, including
cleanup before return, break, and continue. Preserve a returned or yielded value
before releasing the scopes it leaves. Do not infer cleanup anew from syntax in
the eventual C emitter.

Loop-condition temporaries require particular care: existing lifetime behavior
keeps them through the selected body, then cleans them on false exit, backedge, or
transfer as appropriate. A condition region's Yield is not by itself permission
to destroy everything created during the test. Branch-local cleanup and enclosing
scope cleanup must remain distinct. Public tests must exercise these paths before
claiming ownership lowering complete.
