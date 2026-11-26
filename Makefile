# Run from the repository root. Override tools with make DOTNET=/path/to/dotnet.
DOTNET ?= dotnet
PYTHON ?= python3
NODE ?= node
NPM ?= npm
GRADERS ?= graders

.PHONY: release spin01 spin02 spin03 spin04 spin05 spin06 spin07 spin08 spin09 spin10 spin11 test grammar-setup grammar-check syntax

release:
	"$(DOTNET)" build compiler/Lyric.csproj -c Release --nologo --tl:off

# Build first so the walkthrough never silently uses an old executable.
spin01: release
	"$(PYTHON)" tools/spin01.py "$(DOTNET)"

spin02: release
	"$(PYTHON)" tools/spin02.py "$(DOTNET)"

spin03: release
	"$(PYTHON)" tools/spin03.py "$(DOTNET)"

spin04: release
	"$(PYTHON)" tools/spin04.py "$(DOTNET)"

spin05: release
	"$(PYTHON)" tools/spin05.py "$(DOTNET)"

spin06: release
	"$(PYTHON)" tools/spin06.py "$(DOTNET)"

spin07: release
	"$(PYTHON)" tools/spin07.py "$(DOTNET)"

spin08: release
	"$(PYTHON)" tools/spin08.py "$(DOTNET)"

spin09: release
	"$(PYTHON)" tools/spin09.py "$(DOTNET)"

spin10: release
	"$(PYTHON)" tools/spin10.py "$(DOTNET)"

spin11: release
	"$(PYTHON)" tools/spin11.py "$(DOTNET)"

# These are the public retained graders, not a second test implementation.
# Stop immediately if a build or any grader fails.
test:
	"$(PYTHON)" "$(GRADERS)/ch01/check_comparisons.py"
	@set -eu; for configuration in Debug Release; do \
		"$(DOTNET)" build compiler/Lyric.csproj -c "$$configuration" --nologo --tl:off; \
		assembly="compiler/bin/$$configuration/net10.0/Lyric.dll"; \
		"$(PYTHON)" "$(GRADERS)/ch01/grade.py" -- "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch02/unicode.py" -- "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch02/tokens.py" -- "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch03/trees.py" -- "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch03/recognition.py" -- "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch03/boundaries.py" -- "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch04/check.py" --chapter=8 "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch05/run.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch06/run.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch06/unsafe.py" --chapter=8 "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch07/run.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch08/run.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch08/check.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch08/layout.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch08/allocation.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch08/ownership.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch09/ir.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch09/ownership.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch09/c_emission.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch10/canonical.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch10/tuples.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch10/structs.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch10/struct_values.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch10/array_defaults.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch10/borrowed_structs.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch11/generics.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch11/typeof.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch11/matchtype.py" "$(DOTNET)" "$$assembly"; \
	done

# Explicit one-time setup; ordinary grammar checks do not install dependencies.
grammar-setup:
	"$(NPM)" ci --prefix tools/peg --ignore-scripts --no-audit --no-fund

grammar-check: release
	DOTNET="$(DOTNET)" "$(NODE)" tools/peg/test.mjs

syntax: release
	@test -n "$(FILE)" || { printf 'Usage: make syntax FILE=path/to/example.ly\n' >&2; exit 2; }
	DOTNET="$(DOTNET)" "$(NODE)" tools/peg/check.mjs "$(FILE)"

# Source-derived chapter diagrams; no compiler execution or package downloads.
.PHONY: diagrams
diagrams:
	sh tools/common-diagrams/regenerate.sh "$(DOTNET)"

# Existing Pandoc, Chrome, Poppler and a Python with PyMuPDF are prerequisites.
-include .book-tools.mk
export BOOK_NODE BOOK_PLAYWRIGHT_MODULE
BOOK_PYTHON ?= python3.13
.PHONY: book book-pdf book-print-test
book: book-pdf
book-pdf:
	"$(BOOK_PYTHON)" book/build-print.py

book-print-test:
	"$(BOOK_PYTHON)" -m unittest discover -s book -p 'test_*print*.py'
