# ScalarScope Installation Runbook

This document provides step-by-step instructions for installing, upgrading, and uninstalling ScalarScope on a clean Windows machine.

## Prerequisites

### System Requirements
- **OS**: Windows 10 version 1809 (build 17763) or later
- **Architecture**: x64
- **RAM**: 4 GB minimum, 8 GB recommended
- **Disk**: 200 MB free space
- **GPU**: Any DirectX 11 compatible (for SkiaSharp hardware acceleration)

### Runtime Dependencies
ScalarScope is self-contained and includes all required runtimes:
- .NET 9.0 Runtime (bundled)
- Windows App SDK (bundled)
- No additional VC++ redistributables required

## Installation Methods

### Method 1: MSIX Package (Recommended)

1. **Download** the latest `ScalarScope-{version}-x64.msix` from GitHub Releases
2. **Verify** the checksum matches `ScalarScope-{version}-checksums.txt`
3. **Double-click** the MSIX file
4. **Click "Install"** in the App Installer dialog
5. **Wait** for installation to complete (~30 seconds)
6. **Launch** from Start Menu: "ScalarScope"

### Method 2: Microsoft Store (When Available)

1. Open Microsoft Store
2. Search for "ScalarScope"
3. Click "Get" / "Install"
4. Launch from Start Menu

### Method 3: Manual Installation (Development)

```powershell
# Clone and build
git clone https://github.com/mcp-tool-shop-org/scalarscope-desktop.git
cd scalarscope-desktop
dotnet build src/ScalarScope/ScalarScope.csproj -c Release -f net9.0-windows10.0.19041.0

# Run
dotnet run --project src/ScalarScope/ScalarScope.csproj -f net9.0-windows10.0.19041.0
```

## First Run Verification

After installation, verify the app works correctly:

### Step 1: Launch Application
- Open Start Menu
- Type "ScalarScope"
- Click to launch
- **Expected**: App opens with welcome screen within 3 seconds

### Step 2: Load Sample Data
- Click "Open Run" or press Ctrl+O
- Navigate to a sample training run JSON file
- **Expected**: Trajectory visualization appears

### Step 3: Test Playback
- Press Space to start playback
- Press +/- to adjust speed
- **Expected**: Smooth animation, responsive controls

### Step 4: Test Export
- Press S to take screenshot
- **Expected**: Screenshot saved, notification appears

## Upgrade Procedure

### MSIX Upgrade (Automatic)
1. Download new MSIX version
2. Double-click to install
3. App Installer will detect existing installation and upgrade
4. **User data is preserved**

### Upgrade Verification Checklist
- [ ] Version number updated (Help > About)
- [ ] User settings preserved
- [ ] Recent files list preserved
- [ ] No crashes on first launch post-upgrade

## Uninstallation

### Method 1: Settings App
1. Open Settings > Apps > Installed apps
2. Find "ScalarScope"
3. Click "..." > Uninstall
4. Confirm uninstallation

### Method 2: Right-click
1. Open Start Menu
2. Find "ScalarScope"
3. Right-click > Uninstall

### Post-Uninstall State

**Removed**:
- Application files
- Start Menu shortcut
- App registration

**Preserved** (user choice):
- User settings in `%LOCALAPPDATA%\ScalarScope\`
- Export files in user-chosen locations

**To fully remove user data**:
```powershell
Remove-Item -Recurse "$env:LOCALAPPDATA\ScalarScope"
```

## Troubleshooting

### Installation Fails

**Symptom**: "App Installer cannot install this package"

**Solutions**:
1. Enable Developer Mode: Settings > Update & Security > For Developers
2. Or enable sideloading in Group Policy
3. Check Windows version meets minimum requirements

### App Won't Start

**Symptom**: App crashes immediately or shows blank window

**Solutions**:
1. Check GPU drivers are up to date
2. Try: `ScalarScope.exe --software-rendering`
3. Check Event Viewer for crash details
4. Generate support bundle: Help > Create Support Bundle

### Performance Issues

**Symptom**: Choppy playback, high CPU usage

**Solutions**:
1. Ensure GPU acceleration is enabled
2. Reduce export resolution if memory-constrained
3. Close other GPU-intensive applications

## VM Installation Notes

When testing in a virtual machine:

### Hyper-V
- Enable "Enhanced Session Mode" for GPU acceleration
- Allocate at least 4 GB RAM to VM

### VMware
- Enable 3D acceleration in VM settings
- Install VMware Tools for graphics drivers

### VirtualBox
- Enable 3D acceleration
- Install Guest Additions

## Verification Checklist

Use this checklist to certify a clean install:

```
[ ] Downloaded from official source
[ ] Checksum verified
[ ] Installation completed without errors
[ ] App launches within 3 seconds
[ ] Can load training run file
[ ] Playback works smoothly
[ ] Export creates valid PNG
[ ] Keyboard shortcuts respond
[ ] Help menu opens
[ ] About shows correct version
```

## Support

If you encounter issues not covered here:

1. Check [GitHub Issues](https://github.com/mcp-tool-shop-org/scalarscope-desktop/issues)
2. Create a support bundle: Help > Create Support Bundle
3. Open a new issue with the support bundle attached
