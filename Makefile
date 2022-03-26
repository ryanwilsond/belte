PROJDIR:=src/Buckle/Silver/CmdLine
TESTDIR:=src/Buckle/Silver/Buckle.Tests
NETVER:=net5.0
SYSTEM:=win-x64

all: build test

build:
	dotnet build
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Buckle.dll Buckle.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/CmdLine.deps.json CmdLine.deps.json
	cp $(PROJDIR)/bin/Debug/$(NETVER)/CmdLine.dll CmdLine.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/CmdLine.exe buckle.exe
	cp $(PROJDIR)/bin/Debug/$(NETVER)/CmdLine.runtimeconfig.json CmdLine.runtimeconfig.json
	cp $(PROJDIR)/bin/Debug/$(NETVER)/System.Collections.Immutable.dll System.Collections.Immutable.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/System.Runtime.CompilerServices.Unsafe.dll System.Runtime.CompilerServices.Unsafe.dll

test:
	dotnet test $(TESTDIR)/Buckle.Tests.csproj

release:
	dotnet publish $(PROJDIR)/CmdLine.csproj -r $(SYSTEM) -p:PublishSingleFile=true --self-contained true \
		-p:PublishReadyToRunShowWarnings=true -p:IncludeNativeLibrariesForSelfExtract=true --configuration Release
	cp $(PROJDIR)/bin/Release/$(NETVER)/$(SYSTEM)/publish/CmdLine.exe buckle.exe

clean:
	rm -f *.dll
	rm -f *.exe
	rm -f *.json
