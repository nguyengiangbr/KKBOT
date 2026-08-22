# Contributing to KKBOT

Thank you for considering contributing to **KKBOT**! This document outlines the process we use to accept contributions.

## How to contribute

1. **Fork the repository** and clone your fork locally.
2. **Create a branch** for your feature or bug‑fix:
   ```bash
   git checkout -b feature/awesome-feature
   ```
3. **Make your changes**.  Follow the coding conventions defined in the `.editorconfig` file and keep file‑scoped namespaces for all C# files.
4. **Add tests** for any new public API or behaviour.
5. **Run the test suite** locally:
   ```bash
   dotnet test
   ```
   All tests must pass and code coverage should stay above the target set in the CI pipeline.
6. **Commit with a clear message** and push to your fork:
   ```bash
   git push origin feature/awesome-feature
   ```
7. **Open a Pull Request** against the `main` branch of the upstream repository.
   - Provide a concise description of the change.
   - Reference any related issue number (e.g. `#42`).
   - Mention if the PR adds new documentation.

## Review process

- Maintainers will review the PR for correctness, style, and test coverage.
- Small changes may be merged after a single approval; larger changes may require additional reviewers.
- If changes are requested, push new commits to the same branch – the PR will update automatically.

## Style guide & best practices

- **C# version**: target **.NET 10**; use the latest language features.
- **Naming**: PascalCase for types and members, camelCase for local variables.
- **File‑scoped namespaces** for all source files.
- **Async‑await** for all I/O‑bound operations; **no `Thread.Sleep`** – use `Task.Delay`.
- **Error handling**: use the `Result<T>` / `Error` pattern defined in `KKBOT.Domain.Common`.
- **Logging**: use the injected `ILogger<T>` and the `LogEnricher` from `KKBOT.Observability` to add context fields (`InstanceId`, `DeviceId`, etc.).
- **Unit tests**: use **xUnit**, **Moq**, and **FluentAssertions**. Place tests under the corresponding `tests/` project.

## License

By contributing, you agree that your contributions will be licensed under the same **MIT License** as the project.

---

*Happy hacking!*