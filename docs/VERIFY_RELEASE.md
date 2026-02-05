# Release Verification Guide

This guide explains how to verify the authenticity and integrity of ASPIRE Desktop releases.

## Quick Verification

### Step 1: Download Release Files
From the [GitHub Releases](https://github.com/mcp-tool-shop-org/aspire-ai-desktop/releases) page, download:
- `AspireDesktop-{version}-x64.msix` (the installer)
- `AspireDesktop-{version}-checksums.txt` (SHA256 hashes)

### Step 2: Verify Checksum
Open PowerShell and run:
```powershell
# Navigate to downloads folder
cd $env:USERPROFILE\Downloads

# Calculate SHA256 of the downloaded MSIX
Get-FileHash AspireDesktop-1.0.0-rc.1-x64.msix -Algorithm SHA256

# Compare with the value in checksums.txt
Get-Content AspireDesktop-1.0.0-rc.1-checksums.txt
```

The hash values must match exactly.

### Step 3: Verify Certificate (MSIX)
When you double-click the MSIX, Windows App Installer shows:
- Publisher name: should be "ASPIRE Project" or "mcp-tool-shop-org"
- Certificate: Should show trusted or explain the certificate chain

## Checksum Example

```
SHA256 Checksums for AspireDesktop 1.0.0-rc.1
Generated: 2025-02-04

AspireDesktop-1.0.0-rc.1-x64.msix
  e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855

VortexKit.1.0.0-rc.1.nupkg
  a2b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b856
```

## What to Check

| Item | How to Verify |
|------|---------------|
| File integrity | SHA256 checksum match |
| Publisher | Certificate in MSIX |
| Version | Help > About in app |
| Source | Download from official GitHub releases only |

## Red Flags

Do NOT install if:
- ❌ Checksum doesn't match
- ❌ Downloaded from unofficial source
- ❌ Certificate is untrusted or missing
- ❌ File size is significantly different from expected

## Reporting Issues

If you suspect a tampered release:
1. Do NOT install the file
2. Note the download source
3. Report to security@(project email) or [GitHub Security Advisory](https://github.com/mcp-tool-shop-org/aspire-ai-desktop/security/advisories)

## Build Reproducibility

For advanced verification, you can build from source:

```bash
# Clone the exact tag
git clone --branch v1.0.0-rc.1 https://github.com/mcp-tool-shop-org/aspire-ai-desktop.git

# Build
cd aspire-ai-desktop
dotnet build src/AspireDesktop/AspireDesktop.csproj -c Release -f net10.0-windows10.0.19041.0

# The output should match the distributed binaries
```

## CI/CD Pipeline

All releases are built by GitHub Actions:
- Source: Tagged commit on main branch
- Build: Windows-latest runner
- Artifacts: Uploaded to GitHub Releases
- No manual intervention in build process

## Certificate Information

For pre-Store releases, MSIX packages may be self-signed or signed with a test certificate. This is normal for RC releases. The Microsoft Store version will have a Microsoft-trusted certificate.

### Installing Self-Signed MSIX
1. Enable Developer Mode: Settings > Update & Security > For Developers
2. Or: Right-click MSIX > Properties > Digital Signatures > View Certificate > Install Certificate

## Questions?

If you have questions about verification:
- Check [GitHub Discussions](https://github.com/mcp-tool-shop-org/aspire-ai-desktop/discussions)
- Open an [Issue](https://github.com/mcp-tool-shop-org/aspire-ai-desktop/issues)
