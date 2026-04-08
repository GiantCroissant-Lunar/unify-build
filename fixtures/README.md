# UnifyBuild Fixtures

This folder contains repository-local fixtures and dogfooding projects used to exercise engine-specific and mobile build flows.

These projects are intentionally separate from `examples/`:

- `examples/` is the public, documented consumer-facing surface.
- `fixtures/` is for internal validation, experimentation, and repo maintenance.

Current fixtures include:

- `godot-app/` for Godot export and packaging flows
- `unity-app/` for Unity export validation
- `build.config.json` for exercising those fixtures together with mobile build configuration

Use these when validating repository changes, not as the primary onboarding path for consumers.
