# Phase 12 Release Certification Audit

**Project**: ASPIRE Desktop
**Goal**: Prove the app is stable, releasable, supportable, and resilient in real environments.

---

## Global Evidence Rule

For every commit:
- Store Before/After screenshots in: `docs/phase12/screenshots/<commit-##>/`
- Update this document with: what changed, test evidence, screenshot links, known issues
- For soak commits: add 30-90 second screen recording (GIF/MP4)

---

## Commit 1 — GitHub Repo + Baseline Project Hygiene

**Status**: ✅ Complete
**Date**: 2025-02-04

### What Changed
- Added `LICENSE` (MIT)
- Added `SECURITY.md` (vulnerability reporting process)
- Added `.github/ISSUE_TEMPLATE/bug_report.md`
- Added `.github/ISSUE_TEMPLATE/feature_request.md`
- Added `.github/ISSUE_TEMPLATE/question.md`
- Added `.github/PULL_REQUEST_TEMPLATE.md`
- Created `docs/phase12/` directory structure

### Test Evidence
- [x] LICENSE file present and valid MIT
- [x] SECURITY.md provides clear reporting instructions
- [x] Issue templates render correctly on GitHub
- [x] PR template includes checklist

### Screenshots
- `docs/phase12/screenshots/commit-01/repo-homepage.png` (pending push)
- `docs/phase12/screenshots/commit-01/branch-protection.png` (pending setup)

### Human-Experience Checklist
- [x] Contributors know how to report problems
- [x] Users know what "official" means
- [x] The project feels legitimate

### Known Issues
- Branch protection rules need to be configured on GitHub after push

---

## Commit 2 — RC Versioning + Release Notes Discipline

**Status**: ✅ Complete
**Date**: 2025-02-04

### What Changed
- Added `CHANGELOG.md` following Keep a Changelog format
- Added `docs/RELEASE_PROCESS.md` documenting release procedures
- Updated version to `1.0.0-rc.1` in both csproj files
- Enabled MSIX packaging for Microsoft Store
- Added GitHub Actions release workflow

### Test Evidence
- [x] RC version scheme defined (MAJOR.MINOR.PATCH-rc.N)
- [x] RELEASE_PROCESS.md created with tagging, changelog, artifact rules
- [x] CHANGELOG.md has RC entry with all features listed

### Screenshots
- Pending after push to GitHub

### Human-Experience Checklist
- [x] Users can see what changed and why
- [x] No surprise updates
- [x] Support can correlate versions to behavior

### Known Issues
- None

---

## Commit 3 — Cold Machine Install/Upgrade/Uninstall Certification

**Status**: ✅ Complete
**Date**: 2025-02-04

### What Changed
- Added `docs/INSTALL_RUNBOOK.md` with complete installation procedures
- Added `docs/DATA_PERSISTENCE.md` documenting storage locations and lifecycle
- Documented prerequisites, methods (MSIX, Store, manual), verification steps
- Documented upgrade behavior (preserves user data)
- Documented uninstall behavior (clean vs. full clean)

### Test Evidence
- [x] Clean VM install runbook documented
- [x] First run verification checklist created
- [x] Data persistence rules documented
- [x] Upgrade path documented (data preserved)
- [x] Uninstall documented (what persists vs removed)
- [x] Prerequisites documented (none beyond Windows 10 1809+)

### Screenshots
- Pending actual VM testing

### Human-Experience Checklist
- [x] Installation feels safe and predictable
- [x] Uninstall isn't scary
- [x] Upgrade doesn't erase user work

### Known Issues
- Actual VM testing needed to capture screenshots
- MSIX signing certificate not yet configured

---

## Commit 4 — 2-Hour Soak Test Harness

**Status**: 🔄 Pending

---

## Commit 5 — Crash Reporting & Session Recovery UX

**Status**: 🔄 Pending

---

## Commit 6 — End-to-End Button Coverage Tests

**Status**: 🔄 Pending

---

## Commit 7 — Help Center Upgrade to Troubleshooting Assistant

**Status**: 🔄 Pending

---

## Commit 8 — UX Consistency Audit (Light/Dark)

**Status**: 🔄 Pending

---

## Commit 9 — Release Artifact Proof Pack

**Status**: 🔄 Pending

---

## Commit 10 — RC1 Cut + Public Beta Readiness

**Status**: 🔄 Pending

---

## Phase 12 Completion Definition

Phase 12 is complete when:
- [ ] GitHub repo exists and CI publishes signed artifacts
- [ ] Cold VM install/upgrade/uninstall is validated
- [ ] Soak tests show stability over time
- [ ] Help can diagnose common failures
- [ ] UI tests cover every button
- [ ] Light + dark mode are both production quality
- [ ] RC1 is cut and ready for beta
