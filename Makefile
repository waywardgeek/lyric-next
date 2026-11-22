# Run from the repository root. Override tools with make DOTNET=/path/to/dotnet.
DOTNET ?= dotnet
PYTHON ?= python3
NODE ?= node
NPM ?= npm
GRADERS ?= graders

.PHONY: release spin01 spin02 spin03 spin04 spin05 spin06 spin07 test grammar-setup grammar-check syntax

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
		"$(PYTHON)" "$(GRADERS)/ch04/check.py" --chapter=6 "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch05/run.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch06/run.py" "$(DOTNET)" "$$assembly"; \
		"$(PYTHON)" "$(GRADERS)/ch07/run.py" "$(DOTNET)" "$$assembly"; \
	done

# Explicit one-time setup; ordinary grammar checks do not install dependencies.
grammar-setup:
	"$(NPM)" ci --prefix tools/peg --ignore-scripts --no-audit --no-fund

grammar-check: release
	DOTNET="$(DOTNET)" "$(NODE)" tools/peg/test.mjs

syntax: release
	@test -n "$(FILE)" || { printf 'Usage: make syntax FILE=path/to/example.ly\n' >&2; exit 2; }
	DOTNET="$(DOTNET)" "$(NODE)" tools/peg/check.mjs "$(FILE)"
