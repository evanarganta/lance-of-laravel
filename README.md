# Lance of Laravel

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows)
![License](https://img.shields.io/badge/License-MIT-green.svg)

Lance of Laravel handles setup and startup automatically. It validates your project, restores missing Composer and Node dependencies, creates the `.env` file, initializes MySQL databases, starts Laragon services, launches the dev server, and automatically opens your web browser.

## Preview

<img width="100%" alt="Screenshot 2026-08-25 124529" src="https://github.com/user-attachments/assets/71032f95-11ce-4d5d-9881-8b0b70da4a18" />


## Project Layout

```text
Lance of Laravel
│
├── LanceOfLaravel/
│   └── WPF Application (.NET 10)
│
└── Installer/
    ├── LanceOfLaravel.Installer.wixproj
    ├── Package.wxs
    └── Bundle.wxs
```

## Installation

1. Go to [Releases](../../releases) and download `LanceOfLaravelSetup.msi`.
2. Run `LanceOfLaravelSetup.msi`.
3. Choose your desired installation location (defaults to `C:\Program Files (x86)\Lance of Laravel\`).
4. Optionally check the option to create a **Lance of Laravel** desktop shortcut and start menu shortcut.
5. Launch **Lance of Laravel**, click **Select Laravel Project**, and click **Run Project**.

## Building from Source

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10/11
- WiX Toolset v5 (`dotnet tool install --global wix`)

### Build Steps

```powershell
# Clone the repository
git clone https://github.com/evanarganta/lance-of-laravel
cd lance-of-laravel

# Run Application in Development Mode
dotnet run --project LanceOfLaravel/LanceOfLaravel.csproj

# Publish Standalone Executable
dotnet publish LanceOfLaravel/LanceOfLaravel.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# Build Windows Setup Installer (LanceOfLaravelSetup.exe & LanceOfLaravelSetup.msi)
dotnet build Installer/LanceOfLaravel.Installer.wixproj -c Release
```

The compiled setup installer will be output to `Installer\bin\Release\LanceOfLaravelSetup.exe`.

## License

Distributed under the MIT License. See `LICENSE` for details.
