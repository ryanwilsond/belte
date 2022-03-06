# Using Buckle

Buckle is the BELTE programming language compiler.

Call it from the command line giving it your input files like so:

```bash
$ buckle myfile1.ble myfile2.ble
```

This will compile, putting your executable in *a.exe* by default.

| Arg | Description |
|-|-|-|
| -o *filename* | If you specify one file, or linking, produced content will be placed in *filename* instead of *a.exe* |
| -E | Preprocess input files (default compile, assemble, and link) |
| -S | Compile input files |
| -c | Compile and assemble input files |
