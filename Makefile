# Works with bash and powershell
BUCKLE_DIR:=src/Buckle
DEPENDENCY_DIR:=src/Dependencies
CL_DIR:=$(BUCKLE_DIR)/CommandLine
COMPILER_DIR:=$(BUCKLE_DIR)/Compiler
REPL_DIR:=$(BUCKLE_DIR)/Repl
DIAGNOSTICS_DIR:=$(DEPENDENCY_DIR)/Diagnostics

NETVER:=net8.0
SYSTEM:=win-x64
SLN:=Belte.sln
CP=cp
RM=rm

RESOURCES:=Resources
TEST_RESOURCES:=$(COMPILER_DIR).Tests/bin/Debug/$(NETVER)/$(RESOURCES)
SYNTAXPATH:=$(COMPILER_DIR)/CodeAnalysis/Syntax/Syntax.xml
GENERATED_DIR:=$(COMPILER_DIR)/CodeAnalysis/Generated

FLAGS:=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false \
	--sc true -c Release -f $(NETVER)

all: debug
release: prebuild build postbuild
portable: prebuild buildportable postbuildportable
debug: prebuild builddebug postbuilddebug
linux: prebuild buildlinux postbuildlinux
setup: prebuild generate

# Tests the solution
.PHONY: test
test:
	@echo Started testing the Belte solution ...
	@dotnet build $(CL_DIR).Tests/CommandLine.Tests.csproj
	@dotnet build $(COMPILER_DIR).Tests/Compiler.Tests.csproj
	@dotnet build $(DIAGNOSTICS_DIR).Tests/Diagnostics.Tests.csproj
	@$(RM) -f -r $(TEST_RESOURCES)
	@mkdir $(TEST_RESOURCES)
	@$(CP) -a $(COMPILER_DIR)/$(RESOURCES)/. $(TEST_RESOURCES)
	@$(CP) -a $(COMPILER_DIR)/$(RESOURCES)/. $(TEST_RESOURCES)
	@$(CP) -a $(REPL_DIR)/$(RESOURCES)/. $(TEST_RESOURCES)
	@dotnet test $(SLN)
	@echo "    Finished"

# Cleans the solution
clean:
	@dotnet clean $(SLN)
	@$(RM) -f -r bin
	@echo Hard cleaned the solution

# Formats the solution
format:
	@dotnet format $(SLN)
	@echo Formated the solution

# Generates syntax
generate:
	@mkdir -p $(GENERATED_DIR)
	@dotnet run --project $(BUCKLE_DIR)/SourceGenerators/SyntaxGenerator/SyntaxGenerator.csproj --framework $(NETVER) \
		$(SYNTAXPATH) $(GENERATED_DIR)
	@echo Generated compiler source files

prebuild:
	@mkdir -p bin
	@mkdir -p bin/release
	@mkdir -p bin/portable
	@mkdir -p bin/debug
	@mkdir -p bin/linux

postbuild:
	@mv bin/release/CommandLine.exe bin/release/buckle.exe
	@echo "    Finished"

postbuildportable:
	@mv bin/release/CommandLine.exe bin/portable/buckle.exe
	@echo "    Finished"

postbuilddebug:
	@mv bin/debug/CommandLine.exe bin/debug/buckle.exe
	@echo "    Finished"

postbuildlinux:
	@mv bin/linux/CommandLine bin/linux/buckle
	@echo "    Finished"

build:
	@echo "Started building the Buckle project (release) ..."
	@dotnet publish $(CL_DIR)/CommandLine.csproj $(FLAGS) -o bin/release \
		-r $(SYSTEM) -p:PublishReadyToRunShowWarnings=true

buildportable:
	@echo "Started building the Buckle project (portable) ..."
	@dotnet publish $(CL_DIR)/CommandLine.csproj $(FLAGS) -o bin/release

builddebug:
	@echo "Started building the Buckle project (debug) ..."
	@dotnet build $(CL_DIR)/CommandLine.csproj --sc -r $(SYSTEM) -o bin/debug

buildlinux:
	@echo "Started building the Buckle project (portable) ..."
	@dotnet build $(CL_DIR)/CommandLine.csproj $(FLAGS) --sc -o bin/linux
