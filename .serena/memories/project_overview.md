# Project Overview - KyoshinEewViewer for ingen

## Project Purpose
KyoshinEewViewer for ingen is a Japanese disaster prevention application that provides real-time earthquake monitoring. It integrates strong motion network observation data and emergency earthquake warnings/information from Japan Meteorological Agency (JMA) to provide comprehensive earthquake monitoring system.

## Main Features
- Real-time earthquake monitoring with strong motion monitor and emergency earthquake warning display
- Multi-data source support (JMA XML, DM-D.S.S, strong motion networks)
- Geographic information visualization with high-precision map projection
- QZSS disaster crisis management report reception and display
- Customizable workflow system for automated responses and notifications
- Voice alerts with VoiceVox integration
- Cross-platform support (Windows, Linux, macOS)

## Technology Stack
- **Language**: C# .NET 9.0
- **UI Framework**: Avalonia UI with AXAML markup and MVVM pattern
- **Reactive Programming**: ReactiveUI
- **Strong Motion Monitoring**: KyoshinMonitorLib
- **Template Engine**: Scriban
- **Audio**: ManagedBass
- **Modern UI Components**: FluentAvalonia
- **Testing**: xUnit

## Project Type
This is a .NET desktop application with cross-platform capabilities using Avalonia UI framework.