# Chapter 3 syntax-tree shapes

This is the shared parser/semantic-pass boundary, not a semantic type model.
`SyntaxKind` and the relationship declarations belong in Common. Nodes are regular
identity objects, not value-like objects. A source strongly owns all nodes; each
node points back to that source. Ordered parent/child edges are weak and maintained
at both ends. Destruction removes both ends of every relationship. Semantic passes
may traverse this tree but must model backlinks for any new persistent relations.

Every inspection node has `kind`, `text`, and ordered `children`. The names below
are the kebab-case spellings of the closed enum. Unless stated otherwise, text is
null. An anchor is original source spelling, never a normalized or decoded copy.
Within the implementation it is a byte offset/length in the owning source, not a
stored token reference. Child lists below are in order; `?` means optional, `*`
means zero or more, and `+` means one or more. These marks describe this document,
not additional grammar syntax. Optional wrappers are omitted, not null children.

## Roots, names, and literal leaves

- `file`: declarations*. This is also the root of an empty source.
- `expression`: one expression, the expression-inspection root.
- `name`: name or `self` anchor; no children.
- `member`: member name or decimal tuple-selector anchor; receiver/path child.
  Qualified names are nested `member` nodes rooted in `name`.
- `integer`, `float`, `string`, `byte`, `symbol`: complete literal spelling anchor;
  no children. Suffixes remain in numeric spelling; numeric conversion is later.
- `literal`: `true`, `false`, or `null` anchor; no children.
- `unit`: no anchor or children. This differs from a call with no arguments.
- `string-text`: original f-string segment anchor; no children.
- `interpolation`: one expression.
- `interpolated-string`: ordered `string-text`/`interpolation` children, possibly
  empty. Escapes remain spelled in text; this is not decoded literal storage.

## Expressions

- `group`: one parenthesized expression. Preserve grouping explicitly.
- `tuple`: one or more expressions; `array`: zero or more expressions.
- `unary`: operator anchor and operand child.
- `binary`: operator anchor, left operand, right operand.
- `postfix`: `!` or `?` anchor and operand child.
- `call`: callee, then argument expressions*. Thus `f()` and `f(())` differ.
- `generic`: base expression, `type-arguments`. Generic application and the
  subsequent call/member operation are separate nodes.
- `cast`: `as` or `!as` anchor, operand, target type.
- `index`: base, index expression.
- `slice`: base, `lower-bound`, `upper-bound`; both wrappers are always present.
  Each bound wrapper has zero or one expression child.
- `variant-test`: operand, qualified variant name.
- `dictionary`: `entry` children*. Each entry has key expression, value expression.
- `record`: named type, `field-value` children*. Each field-value anchors its field
  name and has one value expression.
- `closure`: `parameters`, `result-type`, `block`.
- `if`: condition, then block, optional else block or nested `if`.
  An `if let` condition is `condition-binding` with pattern and value expression.
- `match`: subject expression, `match-arm` children*. Each arm contains `patterns`,
  optional `guard`, and block. `patterns` holds one or more pattern alternatives;
  `guard` holds one expression.

## Types and constraints

- `type-name`: name anchor; no children.
- `type-member`: member-name anchor, qualifying type child.
- `type-application`: named type, `type-arguments`.
- `type-arguments`: one or more type/role-binding children. `role-binding` anchors
  the role name and contains its type. Roles are allowed only in binding contexts.
- `type-prefix`: `ref` or `gen` anchor, underlying type.
- `optional-type`, `array-type`: one underlying type.
- `type-group`: one grouped type, or tuple element types. A one-element tuple has comma text; grouping and multi-element tuples retain null text.
- `union-type`: two or more types, in source order.
- `function-type`: argument types*, followed by one `result-type` wrapper.
- `result-type`, `annotation`: one type child.
- `type-parameters`: one or more `type-parameter` children; each anchors its name
  and optionally contains a qualified constraint name.
- `capability`: qualified name, optional type-arguments.
- `requirement`: parameter-name anchor, qualified constraint name child.
- `where`: requirement/capability children+.
- `extends`: one capability.

## Patterns and statements

- `wildcard`: `_` anchor; no children.
- `pattern-literal`: literal anchor; no children.
- `pattern-negative`: `-` anchor, integer literal child.
- `pattern-tuple`: one or more pattern children.
- `pattern-name`: qualified name, optional `pattern-arguments`; an explicitly empty
  constructor pattern retains an empty arguments wrapper.
- `pattern-arguments`: pattern children*.
- `block`: statements and `discard-result` markers in source order. A marker
  anchors the first semicolon in the separators following the preceding statement.
  It remains present even when a newline precedes that semicolon. Newlines alone
  do not create a marker. Leading separators do not invent a statement.
- `binding`: `let`/`var` anchor, pattern, optional annotation, optional initializer,
  optional `else` wrapper. The grammar determines which may be omitted.
- `else`: one cleanup block, used by local binding else syntax.
- `expression-statement`: one expression.
- `assignment`: assignment-operator anchor, target expression, value expression.
- `return`: zero or one expression; `yield`: one expression.
- `break`, `continue`: no anchor or children.
- `while`: condition expression, block.
- `for`: pattern, optional `iteration-index`, `iterable`, block.
  `iteration-index` anchors a name and has no children; `iterable` wraps one
  expression. Whether control flow is semantically legal is checked later.

## Declarations

When the grammar permits `pub`, its `visibility` leaf is appended last to that
node's children. It anchors `pub`. `modifier` is likewise a leaf anchoring the
source keyword (`safe`, `final`, or `value`), but occupies the position below.

- `import`: imported-name anchor; optional string child for the source path.
- `type-alias`: alias-name anchor, target type, optional visibility.
- `global`: `let`/`var` anchor, name, optional annotation, initializer, optional
  visibility. No local-binding else block is admitted here.
- `function`: qualified name, optional modifier, optional type-parameters,
  parameters, optional result-type, optional where, optional block, optional
  visibility. A missing block represents a declaration, not an empty body.
- `parameters`: parameter children*. `parameter` anchors its name or `self` and
  has a type child where the grammar requires/allows one. Adapter parameters may
  omit it; ordinary non-self function parameters may not.
- `struct`: name anchor, optional type-parameters, fields*, optional visibility.
- `class`: name anchor, optional value modifier, optional type-parameters, field/
  function members*, optional visibility. Member order is source order.
- `field`: name anchor, type, optional initializer, optional visibility.
- `enum`: name anchor, optional type-parameters, variants*, optional visibility.
- `variant`: name anchor, optional `variant-fields`. The wrapper distinguishes no
  parentheses from explicitly empty parentheses. `variant-fields` holds zero or
  more `variant-field` nodes, each with name anchor and type child.
- `interface`: name anchor, optional type-parameters, optional extends,
  function/interface-field/abstract-relation/cleanup-template members*, optional
  visibility.
- `interface-field`: field-name anchor, owning-type name, field type.
- `cleanup-template`: template-name anchor, relation-kind, block.
- `relation`: qualified relation name, first endpoint, relation-kind, second
  endpoint or many-endpoint wrapper.
- `abstract-relation`: the same shape, but relation-kind may be absent.
- `endpoint`: named type and optional role leaf. `role` anchors the role name.
- `many-endpoint`: one endpoint. `relation-kind` anchors `owns` or `refs`.
- `impl`: optional type-parameters, qualified interface name, type-arguments,
  optional relation-kind, binding members*.
- `relation-binding`: left relation-roles, right relation-roles.
- `relation-roles`: two endpoint children, in source order. Neither endpoint role
  may be collapsed into just its type name.
- `field-binding`: left member path, right member path (`<->`).
- `method-binding`: left member path, right member path or adapter (`=`).
- `adapter`: parameters, result-type, block.

The parser does not infer implicit types, default bodies, storage injection,
ownership meaning, or resolved symbols. Syntax shapes preserve the written facts;
later compiler modules attach semantic meaning through explicit relationships.
