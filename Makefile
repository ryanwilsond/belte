# Works with bash and powershell
COMPILER_DIR:=src/Buckle
BELTE_DIR:=$(COMPILER_DIR)/Belte
BUCKLE_DIR:=$(COMPILER_DIR)/Buckle
DIAGNOSTICS_DIR:=$(COMPILER_DIR)/Diagnostics
REPL_DIR:=$(COMPILER_DIR)/Repl

NETVER:=net8.0
SYSTEM:=win-x64
SLN:=$(COMPILER_DIR)/Buckle.sln
CP=cp
RM=rm

RESOURCES:=Resources
TEST_RESOURCES:=$(BELTE_DIR).Tests/bin/Debug/$(NETVER)/$(RESOURCES)
SYNTAXPATH:=$(BUCKLE_DIR)/CodeAnalysis/Syntax/Syntax.xml
GENERATED_DIR:=$(BUCKLE_DIR)/CodeAnalysis/Generated

FLAGS:=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false \
	--sc true -c Release -f $(NETVER)

all: debug
portable: prebuild generate portablebuild postbuild
release: prebuild generate build postbuild
debug: prebuild generate debugbuild postbuilddebug

# Tests the solution
.PHONY: test
test:
	@echo Started testing the Buckle solution ...
	@dotnet build $(BELTE_DIR).Tests/Belte.Tests.csproj
	@dotnet build $(BUCKLE_DIR).Tests/Buckle.Tests.csproj
	@dotnet build $(DIAGNOSTICS_DIR).Tests/Diagnostics.Tests.csproj
	@$(RM) -f -r $(TEST_RESOURCES)
	@mkdir $(TEST_RESOURCES)
	@$(CP) -a $(BELTE_DIR)/$(RESOURCES)/. $(TEST_RESOURCES)
	@$(CP) -a $(BUCKLE_DIR)/$(RESOURCES)/. $(TEST_RESOURCES)
	@$(CP) -a $(REPL_DIR)/$(RESOURCES)/. $(TEST_RESOURCES)
	@dotnet test $(SLN)
	@echo "    Finished"

# Cleans the solution
clean:
	@dotnet clean $(SLN)
	@echo Hard cleaned the solution

# Formats the solution
format:
	@dotnet format $(SLN)
	@echo Formated the solution

# Generates syntax
generate:
	@mkdir -p $(GENERATED_DIR)
	@dotnet run --project $(BUCKLE_DIR).Generators/Buckle.Generators.csproj --framework $(NETVER) \
		$(SYNTAXPATH) $(GENERATED_DIR)
	@echo Generated compiler source files

prebuild:
	@echo "Started building the Buckle solution (release) ..."
	@mkdir -p bin
	@mkdir -p bin/release
	@mkdir -p bin/debug

postbuild:
	@mv bin/release/Belte.exe bin/release/buckle.exe
	@echo "    Finished"

postbuilddebug:
	@mv bin/debug/Belte.exe bin/debug/buckle.exe
	@echo "    Finished"

build:
	@dotnet publish $(BELTE_DIR)/Belte.csproj $(FLAGS) -r $(SYSTEM) -o bin/release -p:PublishReadyToRunShowWarnings=true

portablebuild:
	@dotnet publish $(BELTE_DIR)/Belte.csproj $(FLAGS) -o bin/release

debugbuild:
	@dotnet build $(BELTE_DIR)/Belte.csproj --sc -r $(SYSTEM) -o bin/debug
