PROJNAME:=CmdLine
BUILDTYPE=DEBUG
EXENAME:=buckle
COMPDIR:=src/Buckle/Silver/Buckle
COMPPROJ:=Buckle
TESTDIR:=src/Buckle/Silver/Buckle.Tests
TESTPROJ:=Buckle.Tests

COMMONPROJ="<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"..\\Buckle\\$(COMPPROJ).csproj\" /></ItemGroup>"
COMMONPROJ+="<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net5.0</TargetFramework>"

CSPROJ=$(COMMONPROJ)
CSPROJFINAL=$(COMMONPROJ)
CSPROJFINAL+="<Configuration>Release</Configuration>"
CSPROJFINAL+="<PublishSingleFile>true</PublishSingleFile><SelfContained>true</SelfContained><RuntimeIdentifier>win-x64</RuntimeIdentifier>"
# CSPROJFINAL+="<PublishTrimmed>true</PublishTrimmed>"
CSPROJFINAL+="<PublishReadyToRun>true</PublishReadyToRun></PropertyGroup></Project>"

ifeq ($(BUILDTYPE), RELEASE)
	CSPROJ+="<Configuration>Release</Configuration>"
endif

CSPROJ+="</PropertyGroup></Project>"
PACKDIR="src/Buckle/Silver/CmdLine"

all: componly cmdonly testsonly copy test

debug: redo all

redo:
	@echo $(CSPROJ) > $(PACKDIR)/$(PROJNAME).csproj
	dotnet build $(PACKDIR)/$(PROJNAME).csproj

install: cleancmd
	@echo $(CSPROJFINAL) > $(PACKDIR)/$(PROJNAME).csproj
	dotnet publish $(PACKDIR)/$(PROJNAME).csproj -r win-x64 -p:PublishSingleFile=true --self-contained true \
		-p:PublishReadyToRunShowWarnings=true -p:IncludeNativeLibrariesForSelfExtract=true
	cp $(PACKDIR)/bin/Release/net5.0/win-x64/publish/$(PROJNAME).exe $(EXENAME).exe

componly:
	dotnet build $(COMPDIR)/$(COMPPROJ).csproj

cmdonly:
	dotnet build $(PACKDIR)/$(PROJNAME).csproj

testsonly:
	dotnet build $(TESTDIR)/$(TESTPROJ).csproj

test:
	dotnet test $(TESTDIR)/$(TESTPROJ).csproj

copy:
	cp $(PACKDIR)/bin/Debug/net5.0/$(PROJNAME).exe $(EXENAME).exe
	cp $(PACKDIR)/obj/Debug/net5.0/$(PROJNAME).dll $(PROJNAME).dll
	cp $(PACKDIR)/bin/Debug/net5.0/$(PROJNAME).deps.json $(PROJNAME).deps.json
	cp $(PACKDIR)/bin/Debug/net5.0/$(PROJNAME).runtimeconfig.json $(PROJNAME).runtimeconfig.json
	cp $(COMPDIR)/bin/Debug/net5.0/$(COMPPROJ).dll $(COMPPROJ).dll

cleancomp:
	rm -fdr $(COMPDIR)/bin
	rm -fdr $(COMPDIR)/obj

cleancmd:
	rm -f $(EXENAME).exe
	rm -f $(PROJNAME).dll
	rm -f $(PROJNAME).deps.json
	rm -f $(PROJNAME).runtimeconfig.json
	rm -f $(COMPPROJ).dll
	rm -fdr $(PACKDIR)/bin
	rm -fdr $(PACKDIR)/obj
	rm -f $(PACKDIR)/$(PROJNAME).csproj

clean: cleancomp cleancmd
