# Description

Please include a summary of the changes and the related issue. Please also include relevant motivation and context.
List any dependencies that are required for this change.

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
- [ ] I have added a test case(s) to the diagnostics tests (e.g. [src/Buckle/Compiler.Tests/Diagnostics/DiagnosticTests.cs](../blob/staging/src/Buckle/Compiler.Tests/Diagnostics/DiagnosticTests.cs)) if any new errors or warnings were added

**If no new syntax was added delete this section:**

Make sure to update each of the following places to support your new syntax. Please delete items that are not relevant.

- [ ] I added my new syntax to [Syntax.xml](../blob/staging/src/Buckle/Compiler/CodeAnalysis/Syntax/Syntax.xml) file
- [ ] I updated the Parser
- [ ] I updated the Binder
- [ ] I updated the Lowerer
- [ ] I updated the Optimizer
- [ ] I updated the Expander
- [ ] I updated the DisplayText
- [ ] I updated the Evaluator
- [ ] I updated the CodeGenerator
~~I updated the CSharpEmitter class~~ (Temporarily not required)
