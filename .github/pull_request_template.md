# Description

Please include a summary of the changes and the related issue. Please also include relevant motivation and context. List any dependencies that are required for this change.

**If applicable:**

Fixes # (issue)

## Type of change

Please delete options that are not relevant.

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] This change requires a documentation update
- [ ] Breaking changes (fix or feature that would cause existing functionality not to work as expected):
  - List the breaking changes (APIs, language features, etc.)
- [ ] Code quality improvements/refactoring

# Checklist

- [ ] My code follows the style guidelines of this project
- [ ] I have performed a self-review of my code
- [ ] I have commented my code in hard-to-understand areas
- [ ] I have added XML comments to any internal or public members and classes
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes
- [ ] I updated the error resource docs (e.g. [src/Buckle/Compiler/Resources/ErrorDescriptionsBU.txt](../blob/staging/src/Buckle/Compiler/Resources/ErrorDescriptionsBU.txt)) if any new errors or warnings were added
- [ ] I updated the diagnostics codes docs ([docs/DiagnosticCodes.md](../blob/staging/docs/DiagnosticCodes.md)) if any new errors or warnings were added
- [ ] I have added a test case(s) to the diagnostics tests (e.g. [src/Buckle/Compiler.Tests/Diagnostics/DiagnosticTests.cs](../blob/staging/src/Buckle/Compiler.Tests/Diagnostics/DiagnosticTests.cs)) if any new errors or warnings were added

**If no new syntax was added delete this section:**

Make sure to update each of the following places to support your new syntax. Please delete items that are not relevant.

- [ ] I added my new syntax to [Syntax.xml](../blob/staging/src/Buckle/Compiler/CodeAnalysis/Syntax/Syntax.xml) file
- [ ] I updated the Parser class
- [ ] I updated the Binder class
- [ ] I updated the Lowerer class
- [ ] I updated the Optimizer class
- [ ] I updated the Expander class
- [ ] I updated the DisplayText class
- [ ] I updated the Evaluator class
- [ ] I updated the CSharpEmitter class
~~I updated the ILEmitter class~~ (Temporarily not required)
