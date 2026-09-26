# 0010 — Developer tooling: a dotnet tool with a browser UI

**Status:** Accepted

## Context
Building documents by editing code and reopening a PDF is slow. Parity requires a live previewer: pages updating as
code changes, an inspector for the frame tree, layout failures shown on the page, and navigation to source.

## Decision
A `dotnet` tool plus a browser UI. `document.Preview()` (or `dotnet rustaveli preview`) starts a local server and
opens the browser: live pages, the frame tree with each frame's measured space, layout errors highlighted in place,
and click-to-source opening the IDE at the line. Hot reload comes from `dotnet watch`.

## Consequences
- One codebase for every operating system, no installers, and it works in dev containers and over SSH.
- Click-to-source requires frames to capture caller file and line when built, which the fluent API must support
  cheaply enough to leave on in development.
