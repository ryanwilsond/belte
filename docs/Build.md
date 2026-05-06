# Build Scripts

> The Builder API is a work in progress and not finalized which is why this document is intentionally brief

> For up to date information on the API, browse the source directly

A build script must define a static non-template method named `Build` with a single parameter of type
`Buckle.Building.Builder` (the assembly is imported automatically) that returns void.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  // ...
}
```

The builder provides an interface to define compilation information, for example:

```belte
using Buckle;
using Buckle.Building;

void Build(Builder builder) {
  builder.AddInput("src");
  builder.buildMode = BuildMode.Dotnet;
}
```
