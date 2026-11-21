# Contributing to CanvasPilot

Thank you for your interest in contributing to CanvasPilot! We welcome contributions from the community and
appreciate your help in making this project better.

## Code of Conduct

This project adheres to a [Code of Conduct][code-of-conduct]. By participating, you are expected to uphold this code.
Please report unacceptable behavior through GitHub.

## How to Contribute

### Reporting Bugs

If you find a bug, please create an issue on GitHub with the following information:

- **Description**: Clear description of the bug
- **Steps to Reproduce**: Detailed steps to reproduce the issue
- **Expected Behavior**: What you expected to happen
- **Actual Behavior**: What actually happened
- **Environment**: Operating system, .NET version, CanvasPilot version
- **Logs**: Any relevant error messages or logs

### Suggesting Features

We welcome feature suggestions! Please create an issue on GitHub with:

- **Feature Description**: Clear description of the proposed feature
- **Use Case**: Why this feature would be useful
- **Proposed Solution**: Your ideas on how to implement it (optional)
- **Alternatives**: Any alternative solutions you've considered (optional)

### Submitting Pull Requests

We follow a standard GitHub workflow for contributions:

1. **Fork** the repository
2. **Clone** your fork locally
3. **Create a branch** for your changes (`git checkout -b feature/my-feature`)
4. **Make your changes** following our coding standards
5. **Test your changes** thoroughly
6. **Commit your changes** with clear commit messages
7. **Push** to your fork
8. **Create a Pull Request** to the main repository

## Development Setup

### Prerequisites

- [.NET SDK][dotnet-download] 10.0 or later (the projects target `net10.0`)
- Git
- A code editor (Visual Studio, VS Code, or Rider recommended)
- Windows is needed only to build the MSI installer (WiX); everything else builds and tests on Windows, Linux, and
  macOS

### Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/demaconsulting/CanvasPilot.git
   cd CanvasPilot
   ```

2. Restore dependencies:

   ```bash
   dotnet tool restore
   dotnet restore
   ```

3. Build and run the tests (zero warnings and all tests must pass):

   ```bash
   pwsh ./build.ps1
   ```

4. Start the application:

   ```bash
   dotnet run --project src/DemaConsulting.CanvasPilot.Desktop
   ```

## Coding Standards

### General Guidelines

- Follow the [C# Coding Conventions][csharp-conventions]
- Use clear, descriptive names for variables, methods, and classes
- Write XML documentation comments for all public, internal, and private members
- Keep methods focused and single-purpose
- Write tests for new functionality

### Code Style

This project enforces code style through `.editorconfig`. Key requirements:

- **Indentation**: 4 spaces for C#, 2 spaces for YAML/JSON/XML
- **Line Endings**: LF (Unix-style)
- **Encoding**: UTF-8
- **Namespaces**: Use file-scoped namespace declarations
- **Braces**: Required for all control statements
- **Naming Conventions**:
  - Interfaces: `IInterfaceName`
  - Classes/Structs/Enums: `PascalCase`
  - Methods/Properties: `PascalCase`
  - Parameters/Local Variables: `camelCase`

### XML Documentation

All members require XML documentation with proper indentation:

```csharp
/// <summary>
///     Brief description of what this does.
/// </summary>
/// <param name="parameter">Description of the parameter.</param>
/// <returns>Description of the return value.</returns>
public int ExampleMethod(string parameter)
{
    // Implementation
}
```

Note the spaces after `///` for proper indentation in summary blocks.

## Testing Guidelines

### Test Framework

We use xUnit v3 (`xunit.v3`) with the Visual Studio test runner for unit, user interface, and OTS tests, NSubstitute
for substitute collaborators, and the Avalonia headless platform for user interface tests. There are three test
projects: `DemaConsulting.CanvasPilot.Tests` (unit, subsystem, and system tests), `DemaConsulting.CanvasPilot.UiTests`
(headless user interface tests), and `OtsSoftwareTests` (tests of the runtime OTS libraries).

### Test Naming Convention

Use the pattern: `ClassName_MethodUnderTest_Scenario_ExpectedBehavior`

Examples:

- `PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly`
- `RollingFileLogger_Constructor_NonPositiveMaximumFileSize_ThrowsArgumentException`
- `MainWindowShell_New_NoProject_TitleIsApplicationNameAndProjectCommandsDisabled`

### Writing Tests

- Write tests that are clear and focused
- Use xUnit assertions such as `Assert.Equal`, `Assert.Empty`, and `Assert.Throws`
- Pass `TestContext.Current.CancellationToken` to asynchronous calls in tests
- Always clean up resources (temporary folders and files) when a test ends
- Link tests to requirements in `docs/reqstream/` when applicable

### Running Tests

```bash
# Build and run all tests
pwsh ./build.ps1

# Run a specific test
dotnet test --filter "FullyQualifiedName~YourTestName"

# Run with coverage
dotnet test --collect "XPlat Code Coverage"
```

## Documentation

### Markdown Guidelines

All markdown files must follow these rules (enforced by LintMark):

- Maximum line length: 120 characters
- Use ATX-style headers (`# Header`)
- Lists must be surrounded by blank lines
- Use reference-style links: `[text][ref]` with `[ref]: url` at document end
- **Exceptions**:
  - `README.md` uses absolute URLs for its images (it is shown on GitHub and other sites outside the repository)
  - AI agent markdown files (`.github/agents/*.agent.md`) use inline links
    `[text](url)` so URLs are visible in agent context

### Spell Checking

All files are spell-checked using TextLint. **Never** add a word to the `.textlint.yaml` word list in order to silence a
spell-checking failure. Doing so defeats the purpose of spell-checking and reduces the quality of the repository.

- If TextLint flags a word that is **misspelled**, fix the spelling in the source file.
- If TextLint flags a word that is a **genuine technical term** (tool name, project identifier, etc.) and is spelled
  correctly, raise a **proposal** (e.g. comment in a pull request) explaining why the word should be added. The
  proposal must be reviewed and approved before the word is added to the list.

## Quality Checks

Before submitting a pull request, ensure all quality checks pass:

### 1. Build and Test

```bash
# Build the project
dotnet build --configuration Release

# Run unit tests
dotnet test --configuration Release
```

All tests must pass with zero warnings.

### 2. Linting

```pwsh
# After making changes: applies dotnet format, markdown, and YAML fixes silently
pwsh ./fix.ps1

# Before submitting a pull request: all linters must pass
pwsh ./lint.ps1
```

### 3. Code Coverage

Maintain or improve code coverage. Use the `--collect "XPlat Code Coverage"` option when running tests.

## Commit Messages

Write clear, concise commit messages:

- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit first line to 72 characters
- Reference issues and pull requests when applicable

Examples:

- `Add support for custom export file names`
- `Fix crash when the project file path is invalid`
- `Update documentation for the Export as PNG command`
- `Refactor argument parsing for better testability`

## Pull Request Process

1. **Update Documentation**: Update relevant documentation for your changes
2. **Add Tests**: Include tests that cover your changes
3. **Run Quality Checks**: Ensure all linters, tests, and builds pass
4. **Submit PR**: Create a pull request with a clear description
5. **Code Review**: Address feedback from maintainers
6. **Merge**: Once approved, a maintainer will merge your PR

### Pull Request Template

When creating a pull request, include:

- **Description**: What changes does this PR introduce?
- **Motivation**: Why are these changes needed?
- **Related Issues**: Link to any related issues
- **Testing**: How have you tested these changes?
- **Checklist**:
  - [ ] Tests added/updated
  - [ ] Documentation updated
  - [ ] All tests pass
  - [ ] Code follows style guidelines
  - [ ] No new warnings introduced

## Requirements Management

CanvasPilot uses [DemaConsulting.ReqStream][reqstream] for requirements traceability:

- All requirements are defined in the files under `docs/reqstream/`, included from `requirements.yaml`
- Each requirement should be linked to test cases
- Run `dotnet reqstream` to generate requirements documentation
- Use the `--enforce` flag to ensure all requirements have test coverage

## Release Process

Releases are managed by project maintainers. The process includes:

1. Version bump in project files
2. Tag the release in Git
3. Build and test across all supported platforms
4. Build the Windows installer (MSI) package
5. Create GitHub release with the installer package, documents, and release notes

## Getting Help

- **Questions**: Use [GitHub Discussions][discussions]
- **Bugs**: Report via [GitHub Issues][issues]
- **Security**: See [SECURITY.md][security] for vulnerability reporting

## License

By contributing to CanvasPilot, you agree that your contributions will be licensed under the MIT License.

Thank you for contributing to CanvasPilot!

[code-of-conduct]: https://github.com/demaconsulting/CanvasPilot/blob/main/CODE_OF_CONDUCT.md
[dotnet-download]: https://dotnet.microsoft.com/download
[csharp-conventions]: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
[reqstream]: https://github.com/demaconsulting/ReqStream
[discussions]: https://github.com/demaconsulting/CanvasPilot/discussions
[issues]: https://github.com/demaconsulting/CanvasPilot/issues
[security]: https://github.com/demaconsulting/CanvasPilot/blob/main/SECURITY.md
