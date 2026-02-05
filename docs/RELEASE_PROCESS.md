# Release Process

This document describes the release process for ScalarScope.

## Version Scheme

We follow [Semantic Versioning](https://semver.org/):

```
MAJOR.MINOR.PATCH[-PRERELEASE]
```

### Pre-release Tags
- `rc.N` - Release Candidate (e.g., `1.0.0-rc.1`)
- `beta.N` - Beta release (e.g., `1.0.0-beta.1`)
- `alpha.N` - Alpha release (e.g., `1.0.0-alpha.1`)

### Current Version
- **1.0.1.1** - Release for Microsoft Store

## Release Checklist

### Before Tagging

1. **Code Freeze**
   - [ ] All features for this release are merged
   - [ ] No known critical bugs
   - [ ] CI is green

2. **Documentation**
   - [ ] CHANGELOG.md updated with all changes
   - [ ] README.md reflects current features
   - [ ] QUICK_REFERENCE.md is accurate

3. **Testing**
   - [ ] All unit tests pass
   - [ ] Manual testing on Windows completed
   - [ ] Soak test passed (for RC+)
   - [ ] Light and dark theme verified

4. **Version Bump**
   - [ ] Update version in `ScalarScope.csproj`
   - [ ] Update version in `VortexKit.csproj`
   - [ ] Commit: "chore: bump version to X.Y.Z"

### Tagging

```bash
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin vX.Y.Z
```

### After Tagging

1. **GitHub Release**
   - [ ] Create release from tag
   - [ ] Copy changelog section to release notes
   - [ ] Attach MSIX installer
   - [ ] Attach checksums file

2. **Artifacts**
   - [ ] MSIX is signed
   - [ ] Checksums generated (SHA256)
   - [ ] Support bundle sample included

3. **Announcement**
   - [ ] Update README badges if needed
   - [ ] Post to relevant channels

## Artifact Naming

```
ScalarScope-{version}-{arch}.msix
ScalarScope-{version}-checksums.txt
```

Example:
```
ScalarScope-1.0.0-rc.1-x64.msix
ScalarScope-1.0.0-rc.1-checksums.txt
```

## Changelog Rules

Every entry should:
1. Be user-facing (not internal refactoring unless significant)
2. Describe what changed, not how
3. Group by: Added, Changed, Deprecated, Removed, Fixed, Security
4. Include issue/PR references where applicable

## Rollback Procedure

If a release has critical issues:

1. Immediately tag a patch release fixing the issue, OR
2. If unfixable quickly:
   ```bash
   git tag -d vX.Y.Z
   git push origin :refs/tags/vX.Y.Z
   ```
3. Remove GitHub release
4. Communicate to users via GitHub issue

## Support Lifecycle

| Version | Status | Support Until |
|---------|--------|---------------|
| 1.0.x   | Active | TBD           |
| 0.x     | EOL    | -             |
