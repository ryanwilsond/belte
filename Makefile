# Works with bash and powershell
PROJDIR:=src/Buckle/Belte
BUCKDIR:=src/Buckle/Buckle
TESTDIR:=src/Buckle/Buckle.Tests
GENRDIR:=src/Buckle/Buckle.Generators
DIAGDIR:=src/Buckle/Diagnostics
REPLDIR:=src/Buckle/Repl
SANDDIR:=src/Sander

NETVER:=net7.0
NETSSTANDVER:=netstandard2.0
SYSTEM:=win-x64
SLN:=src/Buckle/Buckle.sln
SSLN:=src/Sander/Sander.sln
CP=cp
RM=rm

all: build

build: presetup debugbuild debugcopy resources setup
	@echo Finished building Buckle

debugbuild:
	@echo "Started building the Buckle solution (debug) ..."
	@dotnet build $(SLN) -t:rebuild
	@echo "    Finished"

debugcopy:
	@echo Started to copy files for the Buckle solution ...
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Buckle.dll bin/Buckle.dll
	@$(CP) $(REPLDIR)/bin/Debug/$(NETVER)/Repl.dll bin/Repl.dll
	@$(CP) $(GENRDIR)/bin/Debug/$(NETSSTANDVER)/Buckle.Generators.dll bin/Buckle.Generators.dll
# Necessary files to run the execeutable at all, so they need to be in the root directory and not in ./bin
	@$(CP) $(DIAGDIR)/bin/Debug/$(NETVER)/Diagnostics.dll Diagnostics.dll
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.dll Belte.dll
	@-$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.exe buckle.exe
	@-$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte buckle.exe
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.deps.json Belte.deps.json
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.runtimeconfig.json Belte.runtimeconfig.json
	@echo "    Finished"

presetup:
	@echo Started setting up the directory ...
	@$(RM) -f -r bin
	@mkdir bin
	@echo     Finished

setup:
	@echo Started setting up the Buckle solution ...
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Mdb.dll bin/Mono.Cecil.Mdb.dll
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Pdb.dll bin/Mono.Cecil.Pdb.dll
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Rocks.dll bin/Mono.Cecil.Rocks.dll
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.dll bin/Mono.Cecil.dll
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/System.CodeDom.dll bin/System.CodeDom.dll
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/System.Configuration.ConfigurationManager.dll System.Configuration.ConfigurationManager.dll
	@$(CP) $(PROJDIR)/App.config App.config
	@echo     Finished

.PHONY: resources
resources:
	@echo Started coping resources for the Buckle solution ...
	@$(RM) -f -r Resources
	@mkdir Resources
	@$(CP) -a $(PROJDIR)/Resources/. Resources
	@$(CP) -a $(BUCKDIR)/Resources/. Resources
	@$(CP) -a $(REPLDIR)/Resources/. Resources
	@echo "    Finished"

.PHONY: test
test:
	@echo Started testing the Buckle solution ...
	@dotnet test $(TESTDIR)/Buckle.Tests.csproj
	@echo "    Finished"

release: releasebuild resources
	@echo Finished building Buckle

releasebuild:
	@echo "Started building the Buckle solution (release) ..."
	@dotnet publish $(PROJDIR)/Belte.csproj -r $(SYSTEM) -p:PublishSingleFile=true --self-contained true \
		-p:PublishReadyToRunShowWarnings=true -p:IncludeNativeLibrariesForSelfExtract=true --configuration Release
	@$(CP) $(PROJDIR)/bin/Release/$(NETVER)/$(SYSTEM)/publish/Belte.exe buckle.exe
	@echo "    Finished"

clean:
	@$(RM) -f *.dll ||:
	@$(RM) -f *.exe ||:
	@$(RM) -f *.json ||:
	@$(RM) -f *.dot ||:
	@echo Soft cleaned the project

hardclean: clean
	@dotnet clean $(SLN)
	@$(RM) -f -r Resources
	@echo Hard cleaned the project

sandersetup:
	@echo Started setting up the Sander project ...
	@$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.deps.json Sander.deps.json
	@$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.runtimeconfig.json Sander.runtimeconfig.json
	@echo "    Finished"

debugsander:
	@echo "Started to build Sander project (debug) ..."
	@dotnet build $(SSLN)
	@echo "    Finished"

copysander:
	@echo Started to copy files for the Sander project ...
	@$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Buckle.dll Buckle.dll
	@$(CP) $(REPLDIR)/bin/Debug/$(NETVER)/Repl.dll Repl.dll
	@$(CP) $(DIAGDIR)/bin/Debug/$(NETVER)/Diagnostics.dll Diagnostics.dll
	@$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.dll Sander.dll
	@$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.exe sander.exe
	@echo "    Finished"

sander: debugsander copysander sandersetup
	@echo Finished building Sander
