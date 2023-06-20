---
layout: post
title: C# Emitter
---

# C# Emitter

This emitter is very simple as we are just doing one-to-one mapping from Belte code to C# code. To do this, the emitter
walks through every statement and expression (similar to the IL Emitter) and converts it into text representing C# code.
There are a few tricky conversions where the features of Belte and C# do not align perfectly, but it is all just text
manipulation to get it working.

Overall the C# Emitter is very straightforward.

### Mentioned Components

-> [IL Emitter](ILEmitter.md)
