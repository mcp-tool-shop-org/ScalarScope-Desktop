# Button Coverage Matrix

This document tracks all interactive UI elements and their test coverage.

## Overview Tab

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Open File Button | Top toolbar | Opens file picker | ✅ Manual |
| Sample Data Button | Welcome panel | Loads sample run | ✅ Manual |

## Trajectory Tab

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Play/Pause Button | Playback control | Toggle playback | ✅ Manual |
| Step Back Button | Playback control | Decrement time | ✅ Manual |
| Step Forward Button | Playback control | Increment time | ✅ Manual |
| Speed Decrease (-) | Playback control | Slower playback | ✅ Manual |
| Speed Increase (+) | Playback control | Faster playback | ✅ Manual |
| Jump to Start | Playback control | Time = 0 | ✅ Manual |
| Jump to End | Playback control | Time = 1 | ✅ Manual |
| Annotation Toggle - Phases | Annotation panel | Show/hide phases | ✅ Manual |
| Annotation Toggle - Warnings | Annotation panel | Show/hide warnings | ✅ Manual |
| Annotation Toggle - Insights | Annotation panel | Show/hide insights | ✅ Manual |
| Annotation Toggle - Failures | Annotation panel | Show/hide failures | ✅ Manual |

## Scalars Tab

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Metric Toggle Buttons | Metric selector | Select active metrics | ✅ Manual |
| Time Scrubber | Bottom | Adjust current time | ✅ Manual |

## Geometry Tab

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Play/Pause Button | Playback control | Toggle playback | ✅ Manual |
| Eigenvalue Expansion | Eigen panel | Expand/collapse | ✅ Manual |

## Compare Tab

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Left Run Selector | Left panel | Select left run | ✅ Manual |
| Right Run Selector | Right panel | Select right run | ✅ Manual |
| Sync Playback Toggle | Top | Enable/disable sync | ✅ Manual |
| Analytics Toggle | Side panel | Show/hide analytics | ✅ Manual |

## Failures Tab

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Failure Marker Click | Timeline | Select failure | ✅ Manual |
| Severity Filter | Filter bar | Filter by severity | ✅ Manual |
| Category Filter | Filter bar | Filter by category | ✅ Manual |

## Export Panel

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Screenshot Button | Export panel | Export current frame | ✅ Manual |
| Resolution Selector | Export panel | Change export size | ✅ Manual |
| Export Sequence | Export panel | Export frame sequence | ✅ Manual |

## Keyboard Shortcuts

| Shortcut | Expected Result | Test Status |
|----------|-----------------|-------------|
| Space | Play/Pause toggle | ✅ Manual |
| Left Arrow | Step backward | ✅ Manual |
| Right Arrow | Step forward | ✅ Manual |
| Home | Jump to start | ✅ Manual |
| End | Jump to end | ✅ Manual |
| + / = | Increase speed | ✅ Manual |
| - | Decrease speed | ✅ Manual |
| S | Take screenshot | ✅ Manual |
| Ctrl+O | Open file | ✅ Manual |

## Tab Navigation

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Overview Tab | Tab bar | Navigate to overview | ✅ Manual |
| Trajectory Tab | Tab bar | Navigate to trajectory | ✅ Manual |
| Scalars Tab | Tab bar | Navigate to scalars | ✅ Manual |
| Geometry Tab | Tab bar | Navigate to geometry | ✅ Manual |
| Compare Tab | Tab bar | Navigate to compare | ✅ Manual |
| Failures Tab | Tab bar | Navigate to failures | ✅ Manual |

## Recovery Page

| Control | Location | Expected Result | Test Status |
|---------|----------|-----------------|-------------|
| Resume Session | Recovery page | Load last session | ✅ Manual |
| Start Fresh | Recovery page | Clear and continue | ✅ Manual |
| Create Support Bundle | Recovery page | Generate bundle | ✅ Manual |

---

## Summary

- **Total Controls**: 42
- **Tested (Manual)**: 42
- **Tested (Automated)**: 0
- **Coverage**: 100% manual, 0% automated

## Test Execution Log

### Date: 2025-02-04

**Tester**: Phase 12 Certification

**Results**:
- All navigation tabs respond
- All playback controls function
- All annotation toggles work
- Keyboard shortcuts respond
- Export produces valid files

**Issues Found**:
- None blocking

---

## Future Automation

For automated UI testing, consider:
- Appium for cross-platform MAUI testing
- WinAppDriver for Windows-specific testing
- Screenshot comparison for visual regression

```bash
# Example future test command
dotnet test tests/AspireDesktop.UITests --filter Category=ButtonCoverage
```
