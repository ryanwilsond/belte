# Works with bash and powershell
PROJDIR:=src/Buckle/Belte
BUCKDIR:=src/Buckle/Buckle
TESTDIR:=src/Buckle/Buckle.Tests
DIAGDIR:=src/Buckle/Diagnostics
REPLDIR:=src/Buckle/Repl
SANDDIR:=src/Sander

NETVER:=net6.0
SYSTEM:=win-x64
SLN:=src/Buckle/Buckle.sln
SSLN:=src/Sander/Sander.sln
CP=cp
RM=rm

all: build

build: debugbuild debugcopy resources

debugbuild:
	dotnet build $(SLN) -t:rebuild

debugcopy:
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Buckle.dll Buckle.dll
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.dll Belte.dll
	$(CP) $(DIAGDIR)/bin/Debug/$(NETVER)/Diagnostics.dll Diagnostics.dll
	$(CP) $(REPLDIR)/bin/Debug/$(NETVER)/Repl.dll Repl.dll
	-$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.exe buckle.exe
	-$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte buckle.exe

setup:
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.deps.json Belte.deps.json
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Belte.runtimeconfig.json Belte.runtimeconfig.json

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

sandersetup:
	$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.deps.json Sander.deps.json
	$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.runtimeconfig.json Sander.runtimeconfig.json

debugsander:
	dotnet build $(SSLN)

copysander:
	$(CP) $(PROJDIR)/bin/Debug/$(NETVER)/Buckle.dll Buckle.dll
	$(CP) $(REPLDIR)/bin/Debug/$(NETVER)/Repl.dll Repl.dll
	$(CP) $(DIAGDIR)/bin/Debug/$(NETVER)/Diagnostics.dll Diagnostics.dll
	$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.dll Sander.dll
	$(CP) $(SANDDIR)/bin/Debug/$(NETVER)/Sander.exe sander.exe

sander: debugsander copysander
