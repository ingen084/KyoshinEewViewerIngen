# CLAUDE.md

## Language Support

**Japanese Priority**: This project is a Japanese disaster prevention application. All user-facing content must be in Japanese:
- **UI text and messages**: All interface elements, dialogs, and user messages must be in Japanese
- **Log messages**: Write all log messages in Japanese as a characteristic of disaster prevention applications
- **Comments in code**: Write comments in Japanese to maintain consistency with the application domain
- **Error messages**: Display error messages to users in Japanese
- **Terminology**: Earthquake, tsunami, and weather terminology should follow Japan Meteorological Agency (JMA) standards

**Documentation**: Technical documentation and code structure explanations may be in English for international collaboration, but implementation details should prioritize Japanese.

## Project Overview

**KyoshinEewViewer for ingen** - Japanese disaster prevention application
- C# .NET 9.0 + Avalonia UI for cross-platform support
- Monitors seismic activity from JMA and strong motion networks
- Displays real-time earthquake early warnings and earthquake information

## Build Commands

```bash
# Main project
dotnet build src/KyoshinEewViewer/KyoshinEewViewer.csproj

# Desktop version
dotnet build src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj
```

## Architecture

### Series Architecture
Plugin-based modular architecture separating monitoring functions:

- **KyoshinMonitor**: Strong motion network monitoring and earthquake early warnings
- **Earthquake**: JMA XML earthquake information processing
- **Tsunami**: Tsunami warning system
- **Typhoon**: Typhoon tracking
- **Lightning**: Lightning detection
- **Radar**: Weather radar
- **Qzss**: Satellite disaster crisis management reporting

Each Series structure (`src/KyoshinEewViewer/Series/[SeriesName]/`):
- View (AXAML/ViewModel)
- Layer (Map rendering)
- Services (Data processing)
- Models (Data structures)
- SettingPages (Settings UI)
- Templates (Script templates)
- Workflow (Workflow definitions)

### Core Technology Stack
- **Avalonia UI**: AXAML, MVVM, cross-platform
- **ReactiveUI**: Reactive programming
- **KyoshinMonitorLib**: Strong motion monitor processing
- **FluentAvalonia**: Modern UI
- **Scriban**: Template engine
- **ManagedBass**: Audio

## Project Structure

### Main Projects
- `KyoshinEewViewer`: Main application (Series, UI, services)
- `KyoshinEewViewer.Desktop`: Desktop version entry point
- `KyoshinEewViewer.Core`: Shared models, themes, utilities
- `KyoshinEewViewer.Map`: Geographic rendering and map projection
- `KyoshinEewViewer.CustomControl`: Custom UI controls

### Parser Libraries
- `KyoshinEewViewer.JmaXmlParser`: JMA XML parsing
- `KyoshinEewViewer.DCReportParser`: QZSS disaster crisis management report parsing
- `KyoshinEewViewer.CsvSourceGenerator`: CSV dictionary code generation

### Configuration
- `common.props`: Shared MSBuild properties (.NET 9.0, Nullable, etc.)

## Development Patterns

### UI Development (Avalonia)
- MVVM: ViewModels inheriting from `ViewModelBase`
- AXAML markup (Avalonia version of XAML)
- Compiled bindings (enabled by default)
- FluentAvalonia component usage
- **Command Binding**: Avalonia recognizes methods directly as Commands, so `ICommand` implementation is unnecessary

#### Conditional Styling Pattern

For applying different styles based on boolean properties, use the `Classes.` syntax instead of creating converters:

**Use Classes Pattern** (Recommended):

```xml
<Button>
    <Button.Styles>
        <Style Selector="ui|SymbolIcon.muted">
            <Setter Property="Foreground" Value="{DynamicResource EmphasisForegroundColor}" />
        </Style>
    </Button.Styles>
    <ui:SymbolIcon Classes.muted="{Binding IsMuted}" />
</Button>
```

**Style Reusability**:

- When styles need to be reused across multiple controls or files, define them in shared resource dictionaries or at a higher level in the visual tree
- Prefer extracting reusable styles to `UserControl.Styles` or `Window.Styles` sections
- For application-wide styles, consider adding them to theme files or `App.axaml`

**Use Converters Only When**:

- The converter needs to be reused across multiple files or components
- The transformation logic is complex and not just a simple conditional style application

This pattern is more declarative, reduces code, and keeps styling logic in XAML where it belongs.

### Data Processing
- Series-based architecture
- Reactive streams with ReactiveUI/System.Reactive
- Thread-safe data updates
- Geographic data visualization through map layers

### Theme System
- `IntensityTheme`: Seismic intensity display colors
- `WindowTheme`: Application theme
- Theme editor
- System.Text.Json serialization

### Workflow System
Event-driven processing with Scriban templates:
- **Triggers**: Event detection conditions (earthquakes, earthquake early warnings, etc.)
- **Actions**: Response processing (notifications, audio, webhooks, etc.)
- **Events**: Workflow data
- **Templates**: Dynamic content generation with Scriban

## Testing

Using xUnit framework:
- `KyoshinEewViewer.Tests`: Template system tests
- `KyoshinEewViewer.JmaXmlParser.Tests`: XML parsing validation
- `KyoshinEewViewer.DCReportParser.Tests`: QZSS report parsing validation

**Note**: Only run tests for projects existing in the `tests/` directory

### Test Focus
- Focus tests on the core functionality and business logic of classes
- Avoid testing infrastructure code such as event handlers, ResetEvent, or other implementation details
- Test the public API behavior and expected outcomes rather than internal mechanisms
- Prioritize testing actual API interactions, data processing, and error handling scenarios

### Test Organization and Best Practices
- **Consolidate Related Tests**: Group similar test scenarios into comprehensive test methods rather than creating multiple small tests
- **Avoid Redundant Testing**: Do not create separate tests for simple property setters/getters or method chaining that returns the same instance
- **Focus on Integration**: Create tests that verify complete workflows (e.g., builder pattern with full configuration) rather than individual method calls
- **Meaningful Test Names**: Use descriptive Japanese test method names that clearly indicate the scenario being tested
- **Efficient Test Structure**: Use test data arrays or loops to test multiple similar scenarios in a single test method when appropriate

### What NOT to Test
- Simple property assignments that only set and return values
- Method chaining that returns `this` (fluent interface patterns)
- Initial state verification of simple properties
- Infrastructure implementation details (ManualResetEventSlim, internal timers, etc.)
- Constant value definitions
- Enum existence checks

### Test Data and URL Guidelines
**CRITICAL**: When creating test data, NEVER use real production URLs or endpoints to prevent accidental external requests:

#### Forbidden Test Data
- **NEVER** use actual URLs: `api.dmdata.jp`, `data.api.dmdata.jp`, `ws.api.dmdata.jp`, etc.
- **NEVER** use real endpoint hostnames: `ws-tokyo.api.dmdata.jp`, `ws-osaka.api.dmdata.jp`, etc.

#### Required Test Data Patterns
- **Use modified/shortened URLs**: `wsdmdatajp`, `customapidmdatajp`, `customdataapidmdatajp`
- **Use modified endpoints**: `tokyodmdatajp`, `osakadmdatajp` instead of real hostnames
- **Use examplecom**: For general URL testing where domain doesn't matter
- **Use invalid/test schemes**: `invalid-endpoint`, `test-endpoint` for error testing

#### Examples
```csharp
// ✅ GOOD - Modified URLs that won't trigger real requests
var mockResponse = new SocketStartResponse
{
    Websocket = new SocketStartResponse.Info
    {
        Url = "wss://wsdmdatajp/v2/socket"  // Modified, safe
    }
};

// ❌ BAD - Real production URL that could trigger requests
var badResponse = new SocketStartResponse 
{
    Websocket = new SocketStartResponse.Info 
    {
        Url = "wss://ws.api.dmdata.jp/v2/socket"  // Real URL - FORBIDDEN
    }
};
```

This prevents accidental external HTTP/WebSocket requests during testing and protects against unintended API calls to production services.

### Example of Good Test Structure
```csharp
[Fact(DisplayName = "ビルダーパターンのメソッドチェーニングが正常に動作する")]
public void BuilderPattern_MethodChaining_WorksCorrectly()
{
    // Tests complete builder configuration workflow
    // Verifies multiple settings in a single integrated test
}

[Fact(DisplayName = "APIクライアントのメソッド呼び出しが例外をスローしない")]
public void ApiClientMethods_DoNotThrowExceptions()
{
    // Tests multiple API methods using arrays/loops
    // Consolidates parameter validation testing
}
```

## Important Notes

### Scriban Templates
When editing templates, check reference materials:
- [Language Specification](https://raw.githubusercontent.com/scriban/scriban/refs/heads/master/doc/language.md)
- [Built-in Functions](https://raw.githubusercontent.com/scriban/scriban/refs/heads/master/doc/builtins.md)

Aim for simple and understandable implementations.

## Development Guidelines

### Implementation Process
1. **Requirements Clarification**: Always confirm with users when specifications are unclear
2. **Scope Definition**: Verify UI requirements, data structures, and behavior
3. **Implementation Planning**: Present plan to users for approval
4. **Implementation**: Start coding only after confirmation

**No Requirement Guessing** - Always define with users:
- UI design and layout
- Data input/output formats
- Existing system integration points
- Performance requirements
- Error handling

### Implementation Policies
- **DRY Principle**: However, prioritize readability for short code
- **Active Questions**: Don't hesitate to propose or challenge
- **No TODO Left Behind**: Except when instructed otherwise
- **Test Modifications**: Carefully judge the validity of implementation changes
- **Avoid Unnecessary Abstraction**: Don't create excessive abstraction layers. Implement with minimal necessary design
- **Reconsider Correctness**: Even after reaching conclusions, repeatedly review until completely confident before proceeding
- **Early Return**: When using early returns, avoid unnecessary else statements. Use guard clauses to improve readability
- **Avoid Contextual Comments**: Don't write temporary contextual comments like "新規" (new), "追加" (addition), "修正" (modification), or "削除" (deletion) that only make sense in the immediate context. If such comments are written during development, remove them before finalizing the code

### UI Operation Patterns

#### Sub-window Management
Display sub-windows like settings windows through `ISubWindowsService`:
```csharp
var subWindowService = Locator.Current.GetService<ISubWindowsService>();
subWindowService?.ShowSettingWindow();
```

#### Dialog Display
Use `FluentAvalonia.UI.Controls.ContentDialog` for confirmation and error dialogs:
```csharp
// 確認ダイアログ
var result = await new ContentDialog
{
    Title = "確認",
    Content = "この操作を実行しますか？",
    PrimaryButtonText = "はい",
    SecondaryButtonText = "いいえ",
    DefaultButton = ContentDialogButton.Secondary
}.ShowAsync(this);

if (result == ContentDialogResult.Primary)
{
    // 処理を実行
}
```

#### Top-level Controls
Use `KyoshinEewViewerApp.TopLevelControl` as the parent window for file selection and dialog display:
```csharp
if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;
var files = await tlc.StorageProvider.OpenFilePickerAsync(options);

await new ContentDialog
{
    Title = "エラー",
    Content = "操作に失敗しました。",
    CloseButtonText = "OK"
}.ShowAsync(tlc);
```

### Logging Implementation Patterns

#### Standard Service Class Logging Implementation
```csharp
// Implementation using ILogManager (legacy method)
public class SampleService : ReactiveObject, IDisposable
{
    private ILogger Logger { get; }
    
    public SampleService(ILogManager logManager)
    {
        Logger = logManager.GetLogger<SampleService>();
    }
}

// Direct DI implementation of ILogger (recommended)
public class SampleService : ReactiveObject, IDisposable
{
    private ILogger<SampleService> Logger { get; }
    
    public SampleService(ILogger<SampleService> logger)
    {
        Logger = logger;
    }
    
    public async Task ProcessAsync()
    {
        try
        {
            // 処理
            Logger.LogDebug("処理が開始されました");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "処理中にエラーが発生しました");
        }
    }
}
```

#### Static Class Logging Implementation
```csharp
public static class UtilityClass
{
    public static void DoSomething()
    {
        try
        {
            // 処理
        }
        catch (Exception ex)
        {
            LogHost.Default.Error(ex, "処理に失敗しました");
        }
    }
}
```

#### Log Message Rules
- **Japanese Messages**: Write logs in Japanese as a characteristic of disaster prevention applications
- **Dynamic Information**: Include dynamic information in `$"Message {variable}"` format
- **Exception Information**: Include exception information in `Logger.LogError(ex, "Message")` format
- **Log Levels**: Properly use Debug, Info, Warning, Error levels
- **Error Log Usage Policy**: Error logs are sent to developers via Sentry, so use Warning except when bug detection or important issue tracking is specifically needed

#### Log Extension Methods
By including `using KyoshinEewViewer.Core;`, the following extension methods are available for Splat.ILogger:
- `_logger.LogDebug("メッセージ")`
- `_logger.LogInfo("メッセージ")`
- `_logger.LogWarning("メッセージ")`
- `_logger.LogError(exception, "メッセージ")`

This enables Microsoft.Extensions.Logging style log methods.

### Rule Addition Process
Propose adding instructions that could be useful elsewhere to CLAUDE.md for continuous improvement of project rules.

## Design Guidelines

### Notification Template Design
- **Detailed Guide**: `docs/notification-design-guidelines.md`
- **Implementation Examples**: `src/KyoshinEewViewer/Series/*/Templates/*Templates.cs`  
- **Test Patterns**: `tests/KyoshinEewViewer.Tests/Templates/`

## File Format Rules

### End-of-file Newlines
- **All files** must include a newline character at the end
- This ensures proper Git diff display and Unix tool processing
- Recommended to configure editors to automatically add end-of-file newlines

## Task Progress Notation Rules

### TodoWrite activeForm Notation
When using the TodoWrite tool, the `activeForm` parameter should use proper Japanese progressive form:
- **Correct**: `○○を追加中`, `○○の確認中`, `○○を修正中`, `○○の実行中`
- **Incorrect**: `○○を追加している`, `○○を確認している`, `○○を修正している`, `○○を実行している`

This applies to all task progress descriptions to maintain consistency in status reporting.
