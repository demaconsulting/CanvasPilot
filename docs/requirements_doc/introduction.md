# Introduction

This document contains the requirements for the CanvasPilot project.

## Purpose

CanvasPilot is an AI-powered desktop application that helps users create drawings and diagrams with the
CanvasNet library. The current build is the Avalonia application for Windows: folder-based projects, an explorer and a
preview of the project's image source files, PNG export, copy, and drag-out of the picture, persisted settings with
AI provider and speech choices, a chat panel in which messages are composed (typed and dictated text, attached files,
folders, and pictures, and marks drawn on the picture) and sent into a payload that can be inspected, and a Windows
installer. The AI agent that will answer the messages is not part of this version.

## Scope

This requirements document covers:

- The main window, project management, and the project explorer
- Image preview and export
- Persisted user settings, AI provider choices, and speech choices
- Logging capabilities and safe file handling
- The Windows installer
- Multi-platform support
- The runtime and build-time third-party (OTS) items
- Documentation generation and repository quality requirements

## Audience

This document is intended for:

- Software developers working on CanvasPilot
- Quality assurance teams validating requirements
- Project stakeholders reviewing project scope
- Users understanding the tool's capabilities

## References

N/A
