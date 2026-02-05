# ScalarScope Demo Script

**Duration:** 5 minutes
**Audience:** Researchers, reviewers, collaborators
**Goal:** Show that evaluator geometry determines whether conscience internalization is possible

---

## Pre-Demo Setup

1. Have two geometry export files ready:
   - `path_a_orthogonal.json` - Run with orthogonal professors (r ≈ 0)
   - `path_b_correlated.json` - Run with correlated professors (r ≈ 0.87)

2. Launch ScalarScope
3. Window should be at least 1400×900 for best visibility

---

## Script

### 0:00–0:30 | Opening: Load Path A

**[Overview Tab]**

> "ScalarScope is an interactive instrument for studying evaluative learning dynamics. Let me show you what happens when we train a model with orthogonal evaluators—professors who have no shared conception of quality."

- Click **"Load Geometry Run"**
- Select `path_a_orthogonal.json`
- Point out the **Run Summary** card:
  - Condition: "orthogonal"
  - Conscience Tier: shows the final classification
  - Failures: note the count

> "This run used five professors whose evaluations were statistically independent—correlation near zero. Let's see what that looks like in state space."

---

### 0:30–1:00 | Path A Trajectory

**[Trajectory Tab]**

- Let the trajectory play for a few seconds
- **Pause** at around t=50%

> "Watch the trajectory. It's not converging—it's wandering. The model is being pulled in different directions by evaluators who disagree about what 'good' means."

- Toggle **Velocity** on to show the flow field
- Toggle **Curvature** on to highlight instability

> "These orange regions are high curvature—moments where the gradient direction changed sharply. That's the signature of conflicting feedback."

---

### 1:00–1:30 | Annotations Reveal Structure

**[Still on Trajectory Tab]**

- Toggle **Annotations** ON (the switch in the control panel)
- Point to the annotation panel that appears on the right

> "The annotations aren't decoration—they're theoretical claims made visible. Watch:"

- Point to **Phase Labels**: "Here's where dimensionality expanded—the model was exploring, not converging."
- Point to **Curvature Warnings**: "These spikes correspond to phase transitions—moments of evaluator conflict."
- Point to **Eigen Insights** (bottom right): "And here's the key metric: λ₁ is low. The first eigenvalue explains less than half the variance."

> "That means there's no shared axis. The evaluators are genuinely orthogonal in latent space."

---

### 1:30–2:00 | Geometry Tab: The Eigenvalue Story

**[Geometry Tab]**

- Scrub the timeline slowly from start to end
- Watch the eigenvalue bars animate

> "This is the eigenvalue spectrum over time. In a healthy run, you'd see one bar dominate—that's the shared evaluative axis emerging. Here, the bars stay roughly equal."

- Point to the **Effective Dimension** readout

> "Effective dimensionality stays high—around 3 to 4. The model never found a low-dimensional summary of what the professors want, because no such summary exists."

---

### 2:00–2:30 | Load Path B for Comparison

**[Compare Tab]**

> "Now let's see what happens when we change one thing: the geometry of the evaluators."

- Click **Load** under "PATH A (Orthogonal)"
- Select `path_a_orthogonal.json`
- Click **Load** under "PATH B (Correlated)"
- Select `path_b_correlated.json`

> "Same training loop. Same architecture. Same data. The only difference: Path B's professors share a latent quality function. Their correlation is 0.87."

- Let both trajectories play simultaneously

> "Watch the difference. Path A wanders. Path B spirals inward to an attractor."

---

### 2:30–3:30 | Side-by-Side Analysis

**[Still on Compare Tab]**

- **Pause** at around t=70%
- Toggle **Analytics** panel ON if not already visible

> "The analytics panel quantifies what your eye already sees."

Point to each section:

1. **Eigenvalue Analysis**
   > "Path A's λ₁ is around 40%. Path B's is over 85%. That's the shared axis emerging."

2. **Trajectory Metrics**
   > "Path B has lower effective dimensionality and smoother curvature. It found the manifold."

3. **Overall Assessment**
   > "The verdict: Path B shows strong unification. Holdout transfer should succeed—and it does."

- Toggle **Annotations** ON for both canvases

> "With annotations, you can see exactly where the paths diverge. Path A keeps hitting phase transitions. Path B stabilizes early."

---

### 3:30–4:00 | Failures Analysis

**[Failures Tab]**

- Navigate to the Failures tab
- (If Path A is still loaded from earlier, use that; otherwise reload it)

> "Now let's look at where things went wrong. This timeline shows every failure event during training."

- Click on a **critical** failure (red dot) in the timeline

> "Each marker is clickable. This failure happened at t=34%—a calibration collapse. The model's confidence became disconnected from its accuracy."

- Scrub to that time point

> "You can jump to any failure and see the state of the system when it happened. This isn't post-hoc analysis—it's real-time diagnosis."

---

### 4:00–4:30 | Export for Publication

**[Compare Tab]**

- Return to Compare tab with both runs loaded
- Scrub to a visually compelling moment (t ≈ 60%)

> "Everything you see can be exported at publication resolution."

- Click **Export Settings** (or use keyboard shortcut S)
- Show the options briefly

> "You can export single frames at 4K, or frame sequences for videos. The comparison view exports both trajectories with the analytics panel."

- Export a screenshot

> "That figure is now ready for a paper, a talk, or peer review."

---

### 4:30–5:00 | Closing: The Core Claim

**[Back to Compare Tab, paused at a clear divergence point]**

> "Let me leave you with the core claim this instrument makes visible."

Gesture at the two trajectories:

> "Same engine. Same training loop. Same data distribution. Different evaluator geometry. Radically different outcomes."

> "Path A's professors were orthogonal—no shared latent structure. Internalization was impossible, not because the model failed, but because there was nothing coherent to internalize."

> "Path B's professors shared a latent quality axis. The model found it, converged to it, and generalized to held-out evaluators."

> "This isn't a hyperparameter difference. It's a theorem made visible: **Evaluative internalization is possible if and only if evaluators share a latent evaluative manifold.**"

> "ScalarScope lets you see that theorem in action—and falsify it if it's wrong."

---

## Post-Demo Q&A Prompts

If time permits, offer to show:

1. **"Can I see the raw data?"** → Export the JSON, show the schema
2. **"What if I want to test my own evaluators?"** → Explain the geometry export from aspire-engine
3. **"How do I know the visualization isn't misleading?"** → Show that every visual element maps to a theoretical construct (refer to annotation panel)
4. **"Can this scale?"** → Discuss frame sequences, batch export, potential for automated reporting

---

## Key Talking Points (Cheat Sheet)

| If asked about... | Say... |
|-------------------|--------|
| Why side-by-side? | "Same engine, different geometry, different outcome. That's causal isolation." |
| Why annotations? | "Every visual claim should be theoretically grounded and inspectable." |
| Why failures tab? | "Negative results deserve the same visibility as positive ones." |
| Why eigenvalues? | "λ₁ dominance = shared axis = internalization possible. It's the core metric." |
| Why export? | "Research tools should produce publication-ready outputs by default." |

---

## Demo Checklist

- [ ] Two JSON files ready (Path A and Path B)
- [ ] Window sized appropriately (1400×900+)
- [ ] Screen recording software ready (if capturing)
- [ ] Backup exports in case of technical issues
- [ ] Notes visible but not on screen

---

---

## Phase 3 Feature Highlights (Optional Talking Points)

### First-Run Guidance
- Inline hints appear automatically for new users
- Hints explain playback controls and toggle options
- Dismiss with the × button - they won't reappear

### Error Handling Demo
If you want to show robustness:
1. Try loading a non-JSON file → Shows "Wrong File Type" with suggestions
2. Try loading a truncated file → Shows "Truncated File" with recovery steps
3. All errors include "What you can try" actions

### Large File Handling
If demonstrating with 10,000+ timestep files:
- Performance mode auto-enables (lightning bolt icon in legend)
- Frame skipping keeps playback smooth
- "Large file" warning appears in Notes panel

### Export Reliability
- All exports validate data before starting
- Frame sequences show progress
- Cancellation cleans up partial files

---

---

## Phase 4 Feature Highlights (Instrument Trust)

### Invariant Guards
If demonstrating reliability:
- Time values are automatically clamped to [0, 1] - no crashes from out-of-range values
- Empty trajectory shows "no data" message, not crash
- Eigenvalue calculations are consistent across all views

### Cross-View Consistency
- First factor variance is identical in EigenSpectrum and Comparison Analytics
- Interpretation categories match everywhere: "Strong Unification", "Partial", "Orthogonal"
- Effective dimensionality calculation uses single centralized source

### Diagnostics (Help Tab)
- "Run Diagnostics" button shows invariant violation count
- Support bundle captures system state for debugging
- Golden snapshot validation for regression testing

---

*Last updated: Phase 4 Instrument Readiness & Trust*
