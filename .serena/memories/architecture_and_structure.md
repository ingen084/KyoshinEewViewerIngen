# Architecture and Project Structure

## Series Architecture
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

### Test Projects
- `KyoshinEewViewer.Tests`: Template system tests
- `KyoshinEewViewer.JmaXmlParser.Tests`: XML parsing validation
- `KyoshinEewViewer.DCReportParser.Tests`: QZSS report parsing validation