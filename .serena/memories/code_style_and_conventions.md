# Code Style and Conventions

## Language Support Priority
**Japanese Priority**: This project is a Japanese disaster prevention application. All user-facing content must be in Japanese:
- **UI text and messages**: All interface elements, dialogs, and user messages must be in Japanese
- **Log messages**: Write all log messages in Japanese as a characteristic of disaster prevention applications
- **Comments in code**: Write comments in Japanese to maintain consistency with the application domain
- **Error messages**: Display error messages to users in Japanese
- **Terminology**: Earthquake, tsunami, and weather terminology should follow Japan Meteorological Agency (JMA) standards

## C# Code Style (.editorconfig)
- **Indentation**: Tabs for C# files, 4 spaces for other files
- **Line endings**: CRLF
- **Encoding**: UTF-8
- **Final newlines**: Required for all files
- **Braces**: Allman style (new line before opening brace)
- **Namespace**: File-scoped namespace declarations preferred
- **Variables**: Use `var` when type is apparent
- **Access modifiers**: Required for non-interface members
- **Naming**: PascalCase for types, methods, properties; interfaces begin with 'I'

## Project Configuration
- **Target Framework**: .NET 9.0
- **Nullable**: Enabled
- **Language Version**: Latest
- **Avalonia Features**: Compiled bindings enabled by default

## Development Patterns

### UI Development (Avalonia)
- MVVM: ViewModels inheriting from `ViewModelBase`
- AXAML markup (Avalonia version of XAML)
- FluentAvalonia component usage
- **Command Binding**: Avalonia recognizes methods directly as Commands, so `ICommand` implementation is unnecessary

### Logging Implementation
- Use Japanese messages for all logs
- ILogger<T> preferred for services via DI
- Static classes use LogHost.Default
- Error logs sent to developers via Sentry
- Extension methods available: LogDebug, LogInfo, LogWarning, LogError

### File Format Rules
- All files must include a newline character at the end