PROJNAME:=buckle
BUILDTYPE=DEBUG

CSPROJ="<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net5.0</TargetFramework>"
CSPROJFINAL="<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net5.0</TargetFramework>"
CSPROJFINAL+="<Configuration>Release</Configuration>"
CSPROJFINAL+="<PublishSingleFile>true</PublishSingleFile><SelfContained>true</SelfContained><RuntimeIdentifier>win-x64</RuntimeIdentifier>"
# CSPROJFINAL+="<PublishTrimmed>true</PublishTrimmed>"
CSPROJFINAL+="<PublishReadyToRun>true</PublishReadyToRun></PropertyGroup></Project>"

ifeq ($(BUILDTYPE), RELEASE)
	CSPROJ+="<Configuration>Release</Configuration>"
endif

CSPROJ+="</PropertyGroup></Project>"
PACKDIR=.

all:
	@echo $(CSPROJ) > $(PROJNAME).csproj
	dotnet build $(PROJNAME).csproj
	cp bin/Debug/net5.0/$(PROJNAME).exe $(PROJNAME).exe
	cp obj/Debug/net5.0/$(PROJNAME).dll $(PROJNAME).dll
	cp bin/Debug/net5.0/$(PROJNAME).deps.json $(PROJNAME).deps.json
	cp bin/Debug/net5.0/$(PROJNAME).runtimeconfig.json $(PROJNAME).runtimeconfig.json

install:
	@echo $(CSPROJFINAL) > $(PROJNAME).csproj
	dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true -p:PublishReadyToRunShowWarnings=true -p:IncludeNativeLibrariesForSelfExtract=true
	rm -f $(PACKDIR)/$(PROJNAME).exe
	cp bin/Release/net5.0/win-x64/publish/$(PROJNAME).exe $(PACKDIR)/$(PROJNAME).exe

clean:
	rm -f $(PROJNAME).exe
	rm -f $(PROJNAME).dll
	rm -f $(PROJNAME).csproj
	rm -f $(PROJNAME).deps.json
	rm -f $(PROJNAME).runtimeconfig.json
	rm -rfd bin
	rm -rfd obj
