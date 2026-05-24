# Building Buckle Locally

- [Building for Windows](#building-for-windows)
- [Building for Other Systems](#building-for-other-systems)
- [Graphics Library](#graphics-library)

**Required Tools**

- [GNU Make](https://gnuwin32.sourceforge.net/packages/make.htm)
- [.NET SDK 10.0 and .NET Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## Building for Windows

After cloning the repository, run the following command to set up the build directories:

```bash
make setup
```

Then build the project:

```bash
make releasemf
```

If the project fails to compile, try running `make debug && make generate && make releasemf`.

The built executable will be placed at `bin/release/buckle.exe`. It is recommended to add this directory to path.

If moving the executable, make sure that all of the files inside of the release directory are moved together.

Alternatively, a single-file release can be built by running `make release` instead of `make releasemf`. Note that if
this is done, the [compile-time handler](Current/LowLevelFeatures.md#613-compiler-handle) feature will not work.

## Building for Other Systems

Building for other systems has not been tested.

Follow the steps for [building for Windows](#building-for-windows) replacing `make releasemf` with `make portable`.

## Graphics Library

The built-in Graphics Library runs on Mono requiring OpenGL, SDL2, and FreeType6. These libraries should be found
automatically. If FreeType6 is not found automatically, it can be built manually:

Requirements:

- Ensure the Visual C++ Redistributable for Visual Studio 2013 is installed
- Have any installation of Visual Studio 2015 or newer

Steps:

- Download the latest FreeType 2.8 from <https://www.freetype.org/download.html> and unzip the file
- Open `./builds/windows/vc2010/freetype.sln` in Visual Studio
- Modify the solution to target x64 in release mode if it isn't already
- Set the solution Configuration Type to DLL
- Insert the following lines at the very top of `ftoptions.h`

```c
#define FT_EXPORT(x) __declspec(dllexport) x
#define FT_BASE(x) __declspec(dllexport) x
```

- Build
- Rename `./objs/vc2010/x64/freetype28.dll` to `freetype6.dll`
- Replace `bin/release/freetype6.dll` in the repository with this new DLL
