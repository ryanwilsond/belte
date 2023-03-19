# Works with bash and powershell
BELTDIR:=src/Buckle/Belte
BUCKDIR:=src/Buckle/Buckle
DIAGDIR:=src/Buckle/Diagnostics
REPLDIR:=src/Buckle/Repl

NETVER:=net7.0
SYSTEM:=win-x64
SLN:=src/Buckle/Buckle.sln
CP=cp
RM=rm

RESOURCES:=Resources
TESTRESRC:=$(BELTDIR).Tests/bin/Debug/$(NETVER)/$(RESOURCES)

FLAGS:=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false \
	--sc true -c Release -f $(NETVER)

all: prebuild build postbuild
portable: prebuild portablebuild postbuild

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

# Cleans the solution
clean:
	@dotnet clean $(SLN)
	@echo Hard cleaned the project

prebuild:
	@echo "Started building the Buckle solution (release) ..."
	@mkdir -p bin
	@mkdir -p bin/release

postbuild:
	@mv bin/release/Belte.exe bin/release/buckle.exe
	@echo "    Finished"

build:
	@dotnet publish $(BELTDIR)/Belte.csproj $(FLAGS) -r $(SYSTEM) -o bin/release -p:PublishReadyToRunShowWarnings=true

portablebuild:
	@dotnet publish $(BELTDIR)/Belte.csproj $(FLAGS) -o bin/release

