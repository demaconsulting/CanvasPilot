# CanvasPilot

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

## Overview

CanvasPilot is an AI-powered desktop application that helps users create drawings and diagrams with the CanvasNet
library — composing SVG, Mermaid, Vega-Lite, OpenSCAD, and other image sources into standalone images for use in
email, presentations, and other media. It supports conversational interaction by text or speech, using GitHub
Copilot SDK or Ollama as the language model backend.

CanvasPilot is in early development. The current build contains only the command-line scaffold: version and help
display, and self-validation.

The diagram below is a placeholder workflow diagram: replace its TODO boxes (in `docs/images/workflow.svg`,
then re-render `docs/images/workflow.png`) to show how users prepare inputs, run your tool, get its outputs,
and use them.

![Placeholder workflow diagram: replace the TODO boxes to show how users prepare inputs, run your tool, get
its outputs, and use them][workflow]

## Features

The current command-line scaffold provides:

- **Standardized Command-Line Interface**: Context class handling common arguments
  (`--version`, `--help`, `--silent`, `--validate`, `--results`, `--depth`, `--log`)
- **Self-Validation**: Built-in validation tests with TRX/JUnit output
- **Multi-Platform Support**: Builds and runs on Windows, Linux, and macOS
- **Multi-Runtime Support**: Targets .NET 10 and 11
- **Comprehensive CI/CD**: GitHub Actions workflows with quality checks, builds, and
  integration tests
- **Linting Enforcement**: LintMark, TextLint, and yamllint enforced on every CI run
- **Continuous Compliance**: Compliance evidence generated automatically on every CI run, following
  the [Continuous Compliance][link-continuous-compliance] methodology
- **SonarCloud Integration**: Quality gate and security analysis on every build
- **Documentation Generation**: Automated build notes, user guide, code quality reports,
  requirements, justifications, and trace matrix
- **Requirements Traceability**: Requirements linked to passing tests with auto-generated trace matrix

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.CanvasPilot
```

## Usage

```bash
# Default behavior (no arguments) — displays banner and copyright
canvaspilot

# Display version
canvaspilot --version

# Display help
canvaspilot --help

# Run self-validation
canvaspilot --validate

# Save validation results (--result is an accepted alias for --results)
canvaspilot --validate --results results.trx
canvaspilot --validate --result results.xml

# Set heading depth for embedded validation output
canvaspilot --validate --depth 2

# Silent mode with logging
canvaspilot --silent --log output.log
```

## Command-Line Options

| Option | Description |
| --- | --- |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation |
| `--results <file>`, `--result <file>` | Write results to `.trx` (TRX) or `.xml` (JUnit XML) file. |
| `--depth <#>` | Set heading depth for markdown output (default: 1) |
| `--log <file>` | Write output to log file |

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

This behavior enables CI/CD pipelines to detect and surface misconfiguration failures
automatically.

## Self Validation

Running self-validation produces a report containing the following information:

```text
# DEMA Consulting CanvasPilot

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | <version>                                          |
| Machine Name        | <machine-name>                                     |
| OS Version          | <os-version>                                       |
| DotNet Runtime      | <dotnet-runtime-version>                           |
| Time Stamp          | <timestamp> UTC                                    |

✓ CanvasPilot_VersionDisplay - Passed
✓ CanvasPilot_HelpDisplay - Passed

Total Tests: 2
Passed: 2
Failed: 0
```

Each test in the report proves:

- **`CanvasPilot_VersionDisplay`** - `--version` outputs a valid version string.
- **`CanvasPilot_HelpDisplay`** - `--help` outputs usage and options information.

Use `--depth <#>` to control the heading level of the validation output (default: `1`).
This is useful when embedding validation output into a larger markdown document:

```bash
# Embed validation at heading level 2
canvaspilot --validate --depth 2
```

See the [User Guide][link-guide] for more details on the self-validation tests.

On validation failure the tool will exit with a non-zero exit code.

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Comprehensive usage documentation
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Requirements Justifications**: Detailed requirement rationale
- **Trace Matrix**: Requirements to test traceability

## Building

```pwsh
pwsh ./build.ps1
```

## User Guide

The CanvasPilot User Guide is available on the
[CanvasPilot releases page](https://github.com/demaconsulting/CanvasPilot/releases).

## Contributing

See [CONTRIBUTING.md](https://github.com/demaconsulting/CanvasPilot/blob/main/CONTRIBUTING.md) for
guidelines on reporting bugs, suggesting features, and submitting pull requests.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

## Support

- [Report a bug or request a feature](https://github.com/demaconsulting/CanvasPilot/issues)
- [Ask a question or start a discussion](https://github.com/demaconsulting/CanvasPilot/discussions)

<!-- Badge References -->
[workflow]: https://raw.githubusercontent.com/demaconsulting/CanvasPilot/main/docs/images/workflow.png
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/CanvasPilot?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/CanvasPilot?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/CanvasPilot?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/CanvasPilot?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/CanvasPilot/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_CanvasPilot&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_CanvasPilot&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.CanvasPilot?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/CanvasPilot/network/members
[link-stars]: https://github.com/demaconsulting/CanvasPilot/stargazers
[link-contributors]: https://github.com/demaconsulting/CanvasPilot/graphs/contributors
[link-license]: https://github.com/demaconsulting/CanvasPilot/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/CanvasPilot/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_CanvasPilot
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_CanvasPilot
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.CanvasPilot
[link-guide]: https://github.com/demaconsulting/CanvasPilot/blob/main/docs/user_guide/introduction.md
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
