# Works with bash and powershell
PROJDIR:=src/Buckle/Silver/Belte
BUCKDIR:=src/Buckle/Silver/Buckle
TESTDIR:=src/Buckle/Silver/Buckle.Tests
DIAGDIR:=src/Buckle/Silver/Diagnostics
REPLDIR:=src/Buckle/Silver/Repl
NETVER:=net5.0
SYSTEM:=win-x64
SLN:=src/Buckle/Silver/buckle.sln
CP=cp
RM=rm

all: build

build: debugbuild resources

debugbuild:
	dotnet build $(SLN) -t:rebuild
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Buckle.dll Buckle.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.dll Belte.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.exe buckle.exe

setup:
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.deps.json Belte.deps.json
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.runtimeconfig.json Belte.runtimeconfig.json
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/System.Collections.Immutable.dll System.Collections.Immutable.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/System.Runtime.CompilerServices.Unsafe.dll System.Runtime.CompilerServices.Unsafe.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Mdb.dll Mono.Cecil.Mdb.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Pdb.dll Mono.Cecil.Pdb.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Rocks.dll Mono.Cecil.Rocks.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.dll Mono.Cecil.dll
	$(CP) $(DIAGDIR)/bin/Debug/$(NETVER)/Diagnostics.dll Diagnostics.dll
	$(CP) $(REPLDIR)/bin/Debug/$(NETVER)/Repl.dll Repl.dll

.PHONY: resources
resources:
	$(RM) -f -r Resources
	mkdir Resources
	$(CP) -a $(PROJDIR)/Resources/. Resources
	$(CP) -a $(BUCKDIR)/Resources/. Resources
	$(CP) -a $(REPLDIR)/Resources/. Resources

test:
	dotnet test $(TESTDIR)/Buckle.Tests.csproj

release: releasebuild resources

releasebuild:
	dotnet publish $(PROJDIR)/Belte.csproj -r $(SYSTEM) -p:PublishSingleFile=true --self-contained true \
		-p:PublishReadyToRunShowWarnings=true -p:IncludeNativeLibrariesForSelfExtract=true --configuration Release
	$(CP) $(PROJDIR)/bin/Release/$(NETVER)/$(SYSTEM)/publish/Belte.exe buckle.exe

clean:
	$(RM) *.dll
	$(RM) *.exe
	$(RM) *.json
