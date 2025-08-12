# Suggested Commands for Development

## Repository Setup
```bash
# Clone repository with submodules
git clone --recursive https://github.com/ingen084/KyoshinEewViewerIngen.git
cd KyoshinEewViewerIngen

# Initialize submodules (for existing repository)
git submodule update --init --recursive

# Restore dependencies
dotnet restore
```

## Development Commands
```bash
# Run desktop application
dotnet run --project src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj

# Hot reload development (watch mode)
dotnet watch run --project src/KyoshinEewViewer/KyoshinEewViewer.csproj

# Build all projects
dotnet build

# Build main project
dotnet build src/KyoshinEewViewer/KyoshinEewViewer.csproj

# Build desktop version
dotnet build src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj
```

## Testing Commands
```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test tests/KyoshinEewViewer.Tests/
dotnet test tests/KyoshinEewViewer.JmaXmlParser.Tests/
dotnet test tests/KyoshinEewViewer.DCReportParser.Tests/
```

## Production Build Commands
```bash
# Windows x64
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release -r win-x64 -o publish/win-x64 \
  -p:PublishSingleFile=true --self-contained true

# Linux x64
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release -r linux-x64 -o publish/linux-x64 \
  -p:PublishSingleFile=true --self-contained true

# macOS x64
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release -r osx-x64 -o publish/osx-x64 \
  -p:PublishSingleFile=true --self-contained true
```

## System Commands (Linux)
- **List files**: `ls`
- **Change directory**: `cd`
- **Search text**: `grep` or `rg` (ripgrep)
- **Find files**: `find`
- **Git operations**: `git`