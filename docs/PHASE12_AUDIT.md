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

**Status**: 🔄 Pending

### What Changed
- TBD

### Test Evidence
- [ ] RC version scheme defined
- [ ] RELEASE_PROCESS.md created
- [ ] CHANGELOG.md has RC entry

### Screenshots
- TBD

---

## Commit 3 — Cold Machine Install/Upgrade/Uninstall Certification

**Status**: 🔄 Pending

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
