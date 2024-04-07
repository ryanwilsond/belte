# Building Buckle Locally

- [Building for Windows](#building-for-windows)
- [Building for Other Systems](#building-for-other-systems)

**Required Tools**

- [GNU Make](https://gnuwin32.sourceforge.net/packages/make.htm)
- [.NET SDK 8.0 and .NET Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

___

If you haven't already, clone the GitHub repository using the `git clone` command, and enter that directory:

```bash
git clone https://github.com/ryanwilsond/belte.git
cd belte
```

## Building for Windows

Start by setting up the build directories:

```bash
make setup
```

Then build the project:

```bash
make release
```

This will produce runnable executable: `bin/release/buckle.exe`.

## Building for Other Systems

Start by setting up the build directories:

```bash
make setup
```

Then build the project:

```bash
make portable
```

This will produce runnable executable: `bin/portable/buckle.exe`.
