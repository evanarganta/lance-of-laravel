# Laralaunch

Modern, terminal-free, one-click environment launcher for Laravel repositories, using Laragon and built with C# and WPF (.NET 10).
 
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows)
![License](https://img.shields.io/badge/License-MIT-green.svg)

## Overview

Laralaunch eliminates terminal commands, dependency setup hassles, and service startup checks. With a single click, it validates your project, restores missing Composer and Node dependencies, initializes environment files, auto-creates MySQL databases, starts Laragon services, launches your dev server, and automatically opens your web browser.

## Preview

<img width="1083" height="766" alt="Screenshot 2026-08-23 180727" src="https://github.com/user-attachments/assets/229f4b6a-3f14-4475-b97d-cc43881b5ab4" />

## Installation

1. Go to [Releases](../../releases) and download the latest `Laralaunch.exe` single-file executable.
2. Double-click `Laralaunch.exe` to open.
3. Click **Select Laravel Project** to choose your Laravel application directory.
4. Click **Run Project**.

## Building from Source

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10/11

### Build Steps

```powershell
# Clone the repository
git clone https://github.com/evanarganta/laralaunch
cd laravel-launcher

# Run in Development Mode
dotnet run

# Publish Standalone Single-File Executable
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
The compiled single-file binary will be saved in `bin\Release\net10.0-windows\win-x64\publish\Laralaunch.exe`.

## License

Distributed under the MIT License. See `LICENSE` for details.
