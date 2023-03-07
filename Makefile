# Works with bash and powershell
BELTDIR:=src/Buckle/Belte
BUCKDIR:=src/Buckle/Buckle
GENRDIR:=src/Buckle/Buckle.Generators
DIAGDIR:=src/Buckle/Diagnostics
REPLDIR:=src/Buckle/Repl

NETVER:=net7.0
NETSSTANDVER:=netstandard2.0
SYSTEM:=win-x64
SLN:=src/Buckle/Buckle.sln
CP=cp
RM=rm

RESOURCES:=Resources
TESTRESRC:=$(BELTDIR).Tests/bin/Debug/$(NETVER)/$(RESOURCES)

all: build

# Builds the solution as a debug build, and places it in the . directory, in addition to any required setup
build: presetup debugbuild debugcopy resources setup
	@echo Finished building Buckle

# Tests the solution
.PHONY: test
test:
	@echo Started testing the Buckle solution ...
	@dotnet build $(BELTDIR).Tests/Belte.Tests.csproj
	@dotnet build $(BUCKDIR).Tests/Buckle.Tests.csproj
	@dotnet build $(DIAGDIR).Tests/Diagnostics.Tests.csproj
	@$(RM) -f -r $(TESTRESRC)
	@mkdir $(TESTRESRC)
	@$(CP) -a $(BELTDIR)/$(RESOURCES)/. $(TESTRESRC)
	@$(CP) -a $(BUCKDIR)/$(RESOURCES)/. $(TESTRESRC)
	@$(CP) -a $(REPLDIR)/$(RESOURCES)/. $(TESTRESRC)
	@dotnet test $(SLN)
	@echo "    Finished"

# Builds the solution as a release build
release: releasebuild resources
	@echo Finished building Buckle

# Cleans the directory of any debug build artifacts
clean:
	@$(RM) -f *.dll ||:
	@$(RM) -f *.exe ||:
	@$(RM) -f *.json ||:
	@$(RM) -f *.dot ||:
	@echo Soft cleaned the project

# Performs a soft clean, then cleans the solution
hardclean: clean
	@dotnet clean $(SLN)
	@$(RM) -f -r $(RESOURCES)
	@echo Hard cleaned the project

releasebuild:
	@echo "Started building the Buckle solution (release) ..."
	@dotnet publish $(BELTDIR)/Belte.csproj -r $(SYSTEM) -p:PublishSingleFile=true --self-contained true \
		-p:PublishReadyToRunShowWarnings=true -p:IncludeNativeLibrariesForSelfExtract=true --configuration Release
	@mkdir bin
	@mkdir bin/release
	@$(CP) $(BELTDIR)/bin/Release/$(NETVER)/$(SYSTEM)/publish/Belte.exe bin/release/buckle.exe
	@echo "    Finished"

debugbuild:
	@echo "Started building the Buckle solution (debug) ..."
	@dotnet build $(SLN) -t:rebuild
	@echo "    Finished"

debugcopy:
	@echo Started to copy files for the Buckle solution ...
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Buckle.dll $(RESOURCES)/Buckle.dll
	@$(CP) $(REPLDIR)/bin/Debug/$(NETVER)/Repl.dll $(RESOURCES)/Repl.dll
	@$(CP) $(GENRDIR)/bin/Debug/$(NETSSTANDVER)/Buckle.Generators.dll $(RESOURCES)/Buckle.Generators.dll
# Necessary files to run the execeutable at all, so they need to be in the root directory and not in ./$(RESOURCES)
	@$(CP) $(DIAGDIR)/bin/Debug/$(NETVER)/Diagnostics.dll Diagnostics.dll
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Belte.dll Belte.dll
	@-$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Belte.exe buckle.exe
	@-$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Belte buckle.exe
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Belte.deps.json Belte.deps.json
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Belte.runtimeconfig.json Belte.runtimeconfig.json
	@echo "    Finished"

presetup:
	@echo Started setting up the directory ...
	@$(RM) -f -r $(RESOURCES)
	@mkdir $(RESOURCES)
	@echo "    Finished"

setup:
	@echo Started setting up the Buckle solution ...
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Mdb.dll $(RESOURCES)/Mono.Cecil.Mdb.dll
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Pdb.dll $(RESOURCES)/Mono.Cecil.Pdb.dll
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Rocks.dll $(RESOURCES)/Mono.Cecil.Rocks.dll
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/Mono.Cecil.dll $(RESOURCES)/Mono.Cecil.dll
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/System.CodeDom.dll $(RESOURCES)/System.CodeDom.dll
	@$(CP) $(BELTDIR)/bin/Debug/$(NETVER)/System.Configuration.ConfigurationManager.dll System.Configuration.ConfigurationManager.dll
	@$(CP) $(BELTDIR)/App.config App.config
	@echo "    Finished"

.PHONY: resources
resources:
	@echo Started coping resources for the Buckle solution ...
	@$(CP) -a $(BELTDIR)/$(RESOURCES)/. $(RESOURCES)
	@$(CP) -a $(BUCKDIR)/$(RESOURCES)/. $(RESOURCES)
	@$(CP) -a $(REPLDIR)/$(RESOURCES)/. $(RESOURCES)
	@echo "    Finished"
