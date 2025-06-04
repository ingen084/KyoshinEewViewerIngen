# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language Support

**日本語での対応について**: このプロジェクトは日本の地震監視アプリケーションであり、開発者やユーザーからの質問や要求は日本語で行われることが多いです。Claude Code は日本語での質問に対して適切に日本語で回答し、コメントや変数名、ドキュメントなどにおいても日本語の文脈を理解して適切に対応してください。地震・津波・気象などの専門用語についても日本の気象庁の用語に準拠して対応することが重要です。

## Project Overview

**KyoshinEewViewer for ingen** is a real-time earthquake monitoring application for Japan, built with C# .NET 9.0 and Avalonia UI for cross-platform desktop support. The application monitors seismic activity through various data sources including the JMA (Japan Meteorological Agency) and strong motion networks, providing real-time earthquake early warnings and information display.

## Build and Development Commands

### Prerequisites
- .NET SDK 9.0 or higher
- Git with submodules support

### Common Commands

```bash
# Build the main application
dotnet build src/KyoshinEewViewer/KyoshinEewViewer.csproj

# Build the desktop application
dotnet build src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj

# Run the application in development mode
dotnet run --project src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj

# Watch for changes during development
dotnet watch run --project src/KyoshinEewViewer/KyoshinEewViewer.csproj

# Run unit tests
dotnet test tests/KyoshinEewViewer.JmaXmlParser.Tests/
dotnet test tests/KyoshinEewViewer.DCReportParser.Tests/
dotnet test  # Run all tests

# Publish for production (example for Windows x64)
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release \
  -r win-x64 \
  -o publish \
  -p:PublishSingleFile=true \
  --self-contained true
```

### VS Code Integration
- Use **F5** to debug the main application
- Use **Ctrl+Shift+P** → "Tasks: Run Task" → "build", "publish", or "watch"

## Architecture Overview

### Multi-Platform Support
- **Desktop**: Windows, Linux, macOS (primary target)
- **Android**: Mobile application variant  
- **Browser**: WebAssembly version
- **Core**: Shared logic across all platforms

### Modular Series Architecture
The application uses a plugin-like series system where different monitoring capabilities are separate modules:

- **KyoshinMonitor**: Real-time seismic monitoring from strong motion networks
- **Earthquake**: Earthquake information processing from JMA XML feeds
- **Tsunami**: Tsunami warning systems
- **Typhoon**: Typhoon tracking
- **Lightning**: Lightning detection
- **Radar**: Weather radar integration
- **QZSS**: Satellite-based disaster crisis reporting

Each series is located in `src/KyoshinEewViewer/Series/[SeriesName]/` with its own:
- View (AXAML/ViewModel)
- Layer (map rendering)
- Services (data processing)
- Models (data structures)
- SettingPages (configuration UI)

### Data Processing Pipeline
1. **Real-time ingestion**: Multiple data sources (JMA, DM-D.S.S, strong motion networks)
2. **XML parsing**: JMA XML formats using `KyoshinEewViewer.JmaXmlParser`
3. **Map rendering**: Custom projections and geographic data processing
4. **Workflow system**: Automated responses with Scriban templating
5. **Notification system**: Cross-platform notifications and sound alerts

### Key Libraries and Components
- **Avalonia UI**: Cross-platform UI framework with AXAML markup
- **ReactiveUI**: MVVM implementation with reactive programming
- **KyoshinMonitorLib**: Core seismic data processing
- **FluentAvalonia**: Modern UI components
- **Scriban**: Template engine for workflows
- **ManagedBass**: Audio playback
- **ZLinq**: High-performance LINQ operations

## Project Structure

### Core Projects
- `KyoshinEewViewer.Core`: Shared models, themes, and utilities
- `KyoshinEewViewer`: Main application logic and UI
- `KyoshinEewViewer.Desktop`: Desktop-specific implementation and entry point
- `KyoshinEewViewer.Map`: Geographic rendering and map projections
- `KyoshinEewViewer.CustomControl`: Specialized UI controls (intensity displays, map controls)

### Parsing Libraries
- `KyoshinEewViewer.JmaXmlParser`: Japan Meteorological Agency XML parsing
- `KyoshinEewViewer.DCReportParser`: QZSS Disaster Crisis Report parsing
- `KyoshinEewViewer.CsvSourceGenerator`: Code generation for CSV-based data dictionaries

### Configuration Files
- `common.props`: Shared MSBuild properties (.NET 9.0, Nullable enabled, AOT settings)
- `workflows.json`: User workflow configurations (separate from main config)
- `config.json`: Main application settings

## Development Patterns

### UI Development (Avalonia/AXAML)
- Use MVVM pattern with ViewModels inheriting from `ViewModelBase`
- AXAML files for UI markup (Avalonia's XAML variant)
- Compiled bindings enabled by default for performance
- FluentAvalonia components for modern UI elements

### Real-time Data Processing
- Series-based architecture for different data types
- Reactive streams using ReactiveUI/System.Reactive
- Thread-safe data updates with proper synchronization
- Map layers for geographic data visualization

### Configuration and Themes
- `IntensityTheme`: Color schemes for seismic intensity display
- `WindowTheme`: Application visual themes
- Theme editor windows for customization
- Serialization using System.Text.Json with source generators

### Workflow System
The application includes a sophisticated workflow system using Scriban templates:
- **Triggers**: Conditions that start workflows (earthquake detection, EEW reception)
- **Actions**: Responses to triggers (notifications, sounds, webhooks)
- **Events**: Data passed to workflows for template processing
- **Templates**: Scriban-based text processing for dynamic content

## Testing

### Test Projects
- `KyoshinEewViewer.JmaXmlParser.Tests`: XML parsing validation
- `KyoshinEewViewer.DCReportParser.Tests`: QZSS report parsing validation

### Test Patterns
- xUnit framework with standard naming conventions
- Test data located in test project directories
- Mock services for external dependencies

## Git Submodules

The project includes the `jma-code-dictionary` submodule for JMA code definitions:
```bash
git submodule update --init --recursive
```

## Cross-Platform Considerations

### Native Libraries
- Audio libraries (ManagedBass) are platform-specific in `src/KyoshinEewViewer.Desktop/libs/`
- Linux-specific code uses `LINUX` conditional compilation
- Platform detection in `common.props` for build-time configuration

### File Paths and Resources
- Use `Path.Combine()` for cross-platform path handling
- Embedded resources in `Assets/` directories
- Platform-specific resource handling in Desktop project