PROJDIR:=src/Buckle/Silver/Belte
TESTDIR:=src/Buckle/Silver/Buckle.Tests
NETVER:=net5.0
SYSTEM:=win-x64
SLN:=src/Buckle/Silver/buckle.sln

all: build

build:
	dotnet build $(SLN) -t:rebuild
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Buckle.dll Buckle.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Belte.dll Belte.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Belte.exe buckle.exe
	rm -f -r Resources
	cp -r $(PROJDIR)/Resources Resources

setup:
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Belte.deps.json Belte.deps.json
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Belte.runtimeconfig.json Belte.runtimeconfig.json
	cp $(PROJDIR)/bin/Debug/$(NETVER)/System.Collections.Immutable.dll System.Collections.Immutable.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/System.Runtime.CompilerServices.Unsafe.dll System.Runtime.CompilerServices.Unsafe.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Mdb.dll Mono.Cecil.Mdb.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Pdb.dll Mono.Cecil.Pdb.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.Rocks.dll Mono.Cecil.Rocks.dll
	cp $(PROJDIR)/bin/Debug/$(NETVER)/Mono.Cecil.dll Mono.Cecil.dll

test:
	dotnet test $(TESTDIR)/Buckle.Tests.csproj

release:
	dotnet publish $(PROJDIR)/Belte.csproj -r $(SYSTEM) -p:PublishSingleFile=true --self-contained true \
		-p:PublishReadyToRunShowWarnings=true -p:IncludeNativeLibrariesForSelfExtract=true --configuration Release
	cp $(PROJDIR)/bin/Release/$(NETVER)/$(SYSTEM)/publish/Belte.exe buckle.exe
	rm -f -r Resources
	cp -r $(PROJDIR)/Resources Resources

clean:
	rm -f *.dll
	rm -f *.exe
	rm -f *.json
