# Debugging a Program

This sample shows how debugging can be set up with VSCode. The only requirement
is that the Buckle compiler is added to your path environment variable.

[.vscode/tasks.json](.vscode/tasks.json) shows an example of how to invoke the
compiler to produce a PDB. [.vscode/launch.json](.vscode/launch.json) has
options about debugging your program. Notably, breakpoints are not currently
supported by the Belte VSCode extension, so it can be helpful to set
`stopAtEntry` to true. Otherwise, you can still pause a running program to start
stepping, or the program will automatically pause if an exception is thrown.

In addition to being able to step through your code, the PDB contains
information about method locals which can be viewed in the debug panel.
