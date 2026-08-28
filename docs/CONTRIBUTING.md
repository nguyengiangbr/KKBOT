# Contributing to KKBOT

Thank you for your interest in contributing to KKBOT! This document provides guidelines for contributing to the project.

## Code of Conduct

Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

## How to Contribute

### Reporting Issues

Before creating an issue, please check if it already exists. When creating an issue, please include:

- Clear title and description
- Steps to reproduce
- Expected vs actual behavior
- Environment details (OS, .NET version, etc.)
- Relevant logs/screenshots

### Pull Request Process

1. **Fork** the repository
2. **Create a feature branch** from `main`
3. **Make your changes** with tests
4. **Run tests** to ensure nothing breaks
5. **Submit a Pull Request** with clear description

### Commit Message Convention

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>

[optional body]

[optional footer]
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, missing semi-colons, etc.
- `refactor`: Code restructuring
- `perf`: Performance improvement
- `test`: Adding tests
- `chore`: Maintenance

Example:
```
feat(agent): add LLM-powered planner with streaming support
fix(perception): handle null template matches gracefully
docs(arch): update architecture diagram for WASM sandbox
```

## Development Setup

### Prerequisites

- .NET 10 SDK
- Docker 24+ (for integration tests)
- Redis 7+ (for coordination features)
- Ollama (for AI features)

### Local Development

```bash
# Clone and build
git clone https://github.com/your-org/kkbot.git
cd kkbot
dotnet restore
dotnet build

# Run tests
dotnet test

# Run API
dotnet run --project src/KKBOT.API

# Run Dashboard
dotnet run --project src/KKBOT.UI.Dashboard
```

### Code Style

We use:
- **EditorConfig** for consistent formatting
- **StyleCop.Analyzers** for code style
- **Nullable** reference types enabled
- **Async/await** preferred over sync

Run formatter:
```bash
dotnet format
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/KKBOT.Orchestration.Integration.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Adding New Game Plugins

1. Create new project: `src/KKBOT.Plugin.YourModule`
2. Reference required abstractions
3. Implement `IGameModule`
4. Add to solution
5. Add tests in `tests/KKBOT.Plugin.YourModule.Tests`

## Reporting Security Issues

Please **do not** report security vulnerabilities through public GitHub issues. Instead, email security@kkbot.example.com.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Questions?

- Open a [Discussion](https://github.com/your-org/kkbot/discussions)
- Check [Documentation](https://docs.kkbot.example.com)

Thank you for contributing to KKBOT!