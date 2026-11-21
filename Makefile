# Run from the repository root. Override tools with make DOTNET=/path/to/dotnet.
DOTNET ?= dotnet
PYTHON ?= python3
GRADERS ?= graders

.PHONY: release spin01 spin02 test

release:
	"$(DOTNET)" build compiler/Lyric.csproj -c Release --nologo --tl:off

# Build first so the walkthrough never silently uses an old executable.
spin01: release
	"$(PYTHON)" tools/spin01.py "$(DOTNET)"

spin02: release
	"$(PYTHON)" tools/spin02.py "$(DOTNET)"

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
	done
