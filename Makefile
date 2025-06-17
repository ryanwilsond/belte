# Works with bash and powershell
BUCKLE_DIR:=src/Buckle
DEPENDENCY_DIR:=src/Dependencies
RUNTIME_DIR:=src/Belte.Runtime
CL_DIR:=$(BUCKLE_DIR)/CommandLine
COMPILER_DIR:=$(BUCKLE_DIR)/Compiler
REPL_DIR:=$(BUCKLE_DIR)/Repl
DIAGNOSTICS_DIR:=$(DEPENDENCY_DIR)/Diagnostics
DEBUG_DIR:=bin/debug
RELEASE_DIR:=bin/release

NETVER:=net9.0
SYSTEM:=win-x64
SLN:=Belte.sln

RESOURCES:=Resources
TEST_RESOURCES:=$(COMPILER_DIR).Tests/bin/Debug/$(NETVER)/$(RESOURCES)
SYNTAXPATH:=$(COMPILER_DIR)/CodeAnalysis/Syntax/Syntax.xml
BOUNDNODESPATH:=$(COMPILER_DIR)/CodeAnalysis/Binding/BoundTree/BoundNodes.xml
GENERATED_DIR:=$(COMPILER_DIR)/CodeAnalysis/Generated

PUBLISH_FLAGS:=-p:DebugType=None -p:DebugSymbols=false --sc true -c Release -f $(NETVER)
SINGLE_FILE_FLAGS:=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
FLAGS:=$(PUBLISH_FLAGS) $(SINGLE_FILE_FLAGS)

ifeq ($(OS), Windows_NT)
	RM:=powershell -Command "Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"
	CP:=powershell -Command "Copy-Item -Recurse"
	MV:=powershell -Command "Move-Item -Force"
	MKDIR:= powershell -Command "New-Item -ItemType Directory -Force"
else
	RM:=rm -rf
	CP:=cp
	MV:=mv
	MKDIR:=mkdir -p
endif

all: debug
release: prebuild copydlls build postbuild
portable: prebuild buildportable postbuildportable
debug: prebuild builddebug postbuilddebug
linux: prebuild buildlinux postbuildlinux
setup: prebuild generate

.PHONY: test

# Tests the solution
test:
	@echo Started testing the Belte solution ...
	@dotnet build $(CL_DIR).Tests/CommandLine.Tests.csproj
	@dotnet build $(COMPILER_DIR).Tests/Compiler.Tests.csproj
	@dotnet build $(DIAGNOSTICS_DIR).Tests/Diagnostics.Tests.csproj
	@dotnet test $(SLN)
	@echo "    Finished"

# Cleans the solution
clean:
	@dotnet clean $(SLN)
	@$(RM) bin
	@$(RM) lib
	@echo Hard cleaned the solution

# Formats the solution
format:
	@dotnet format $(SLN)
	@echo Formated the solution

# Generates syntax
generate:
	@$(MKDIR) $(GENERATED_DIR)
	@dotnet run --project $(BUCKLE_DIR)/SourceGenerators/SyntaxGenerator/SyntaxGenerator.csproj --framework $(NETVER) \
		$(SYNTAXPATH) $(GENERATED_DIR)
	@dotnet run --project $(BUCKLE_DIR)/SourceGenerators/BoundTreeGenerator/BoundTreeGenerator.csproj \
		--framework $(NETVER) $(BOUNDNODESPATH) $(GENERATED_DIR)
	@echo Generated compiler source files

# Builds the Belte.Runtime DLL
runtime:
	@$(MKDIR) lib
	@dotnet publish $(PUBLISH_FLAGS) $(RUNTIME_DIR)/Belte.Runtime.csproj -o lib
	@echo Built Belte.Runtime.dll

prebuild:
	@$(MKDIR) bin
	@$(MKDIR) bin/release
	@$(MKDIR) bin/portable
	@$(MKDIR) bin/debug
	@$(MKDIR) bin/linux

postbuild:
	@$(MV) bin/release/CommandLine.exe bin/release/buckle.exe
	@echo "    Finished"

postbuildportable:
	@$(MV) bin/release/CommandLine.exe bin/portable/buckle.exe
	@echo "    Finished"

postbuilddebug:
	@$(MV) bin/debug/CommandLine.exe bin/debug/buckle.exe
	@echo "    Finished"

postbuildlinux:
	@$(MV) bin/linux/CommandLine bin/linux/buckle
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
	@echo "Started building the Buckle project (linux) ..."
	@dotnet build $(CL_DIR)/CommandLine.csproj --sc -o bin/linux

copydlls:
ifeq (,$(wildcard $(RELEASE_DIR)/freetype6.dll))
	@$(MAKE) builddebug
	@$(MAKE) copydllscore
endif

copydllscore:
	@$(CP) $(DEBUG_DIR)/freetype6.dll $(RELEASE_DIR)/
	@$(CP) $(DEBUG_DIR)/openal.dll $(RELEASE_DIR)/
	@$(CP) $(DEBUG_DIR)/SDL2.dll $(RELEASE_DIR)/
