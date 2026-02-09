# ScalarScope: 5-Phase UI/UX & Humanization Plan

> **Goal:** Transform ScalarScope from technically impressive but inert → intuitive, expressive, and narratively coherent without diluting rigor.

---

## PHASE 1 — Establish Life & Orientation

> *"The app must feel alive before it feels correct."*

**Objective:** Eliminate empty states and immediately orient the user.

### Commits (5)

- [ ] **1.1 Add Canonical Startup Vector**
  - Introduce a default animated latent vector on app load
  - Loops gently, non-interactive, clearly labeled as "demo state"

- [ ] **1.2 Replace All Empty Visualizations**
  - Trajectory, Scalars, Geometry load with synthetic exemplar data
  - Clearly watermarked: "Illustrative Example"

- [ ] **1.3 Global "System Is Alive" Signal**
  - Subtle background motion (vector drift, eigenvalue shimmer)
  - Motion pauses when real data loads

- [ ] **1.4 Rewrite Empty-State Copy**
  - Replace "Load a geometry run…" with:
  - *"You're viewing a reference trajectory. Load a run to replace it."*

- [ ] **1.5 First-Run Soft Overlay**
  - One-time overlay:
  - *"ScalarScope visualizes how internal evaluators align during learning."*

**Exit Criteria:**
A new user never sees a blank grid or dead canvas.

---

## PHASE 2 — Narrative Before Control

> *"Users ask questions before they adjust parameters."*

**Objective:** Reframe UI from controls → intentions.

### Commits (5)

- [ ] **2.1 Intent-Labeled Toggles**
  - Velocity → "How fast is learning changing?"
  - Curvature → "Is the model revising itself?"
  - Professors → "Are evaluators aligned?"

- [ ] **2.2 Inline Microcopy (≤12 words each)**
  - Every toggle gets a one-line explanation
  - Appears on hover or focus

- [ ] **2.3 Progressive Disclosure**
  - Hide advanced toggles behind "More Analysis"
  - Default view shows only 3–4 core signals

- [ ] **2.4 Contextual Tooltips**
  - Tooltips reference what changed, not definitions
  - Example: "Spike here indicates evaluator disagreement"

- [ ] **2.5 Narrative Section Headers**
  - Replace "Trajectory" with:
  - *"How learning unfolded over time"*

**Exit Criteria:**
A non-expert can explain what they're seeing without reading docs.

---

## PHASE 3 — Make Comparison the Star

> *"Difference is the insight."*

**Objective:** Turn Compare into the emotional and analytical centerpiece.

### Commits (5)

- [ ] **3.1 Add Middle 'Delta' Column**
  - Between Path A and Path B
  - Shows quantitative + qualitative deltas

- [ ] **3.2 Highlight Causal Language**
  - Example:
  - *"Correlation caused eigenvalue collapse by step 42"*

- [ ] **3.3 Temporal Alignment Controls**
  - Sync timelines by epoch, convergence, or failure
  - Default: "Align by convergence onset"

- [ ] **3.4 Visual Contrast Amplification**
  - Color saturation increases where divergence matters
  - Calm visuals where paths behave similarly

- [ ] **3.5 One-Sentence Comparative Summary**
  - Auto-generated takeaway at bottom:
  - *"Shared evaluative structure produced faster, narrower convergence."*

**Exit Criteria:**
Compare answers "why did these runs differ?" in under 10 seconds.

---

## PHASE 4 — Teach Without Teaching

> *"The interface should explain itself."*

**Objective:** Move knowledge from Help into the workflow.

### Commits (5)

- [ ] **4.1 Inline Insight Callouts**
  - Appear at moments of phase change, collapse, or instability
  - Dismissible, never modal

- [ ] **4.2 Visual Grammar Consistency**
  - Color = epistemic role (evaluator, axis, instability)
  - Shape = dynamical behavior (spiral, kink, plateau)

- [ ] **4.3 Failure Reframing**
  - Rename "Failures" → "Stability & Events"
  - Show why failure didn't happen when absent

- [ ] **4.4 First-Occurrence Highlighting**
  - First eigenvalue dominance, first curvature spike, etc.
  - Small glow + explanation

- [ ] **4.5 Help Page → Reference Mode**
  - Remove onboarding tone
  - Reframe as "Interpretation Guide"

**Exit Criteria:**
Users learn the system by using it, not by reading.

---

## PHASE 5 — Emotional Polish & Trust

> *"Great scientific tools feel calm, confident, and inevitable."*

**Objective:** Make ScalarScope feel finished, trustworthy, and professional.

### Commits (5)

- [ ] **5.1 Motion Rationalization**
  - Every animation has a semantic reason
  - No decorative motion without meaning

- [ ] **5.2 State Transitions**
  - Loading → analysis → comparison feel continuous
  - No hard visual resets

- [ ] **5.3 Language Audit**
  - Replace passive system messages with confident observations
  - "Detected" → "Observed"

- [ ] **5.4 Accessibility as First-Class**
  - Motion reduction preserves meaning
  - High contrast mode tested on all views

- [ ] **5.5 Demo Narrative Mode**
  - One-click "Guided Run"
  - Plays through Path A vs B with annotations

**Exit Criteria:**
ScalarScope feels like a research instrument, not a prototype.

---

## Final Framing

> You are not "fixing" a broken app.
> You are aligning an expert-built system with human cognition.

The math is already doing the right thing.
Now the interface needs to tell the same story the math is telling.

---

## Summary

| Phase | Theme | Commits | Focus |
|-------|-------|---------|-------|
| 1 | Life & Orientation | 5 | Eliminate empty states |
| 2 | Narrative Before Control | 5 | Intent-driven UI |
| 3 | Make Comparison the Star | 5 | Delta as centerpiece |
| 4 | Teach Without Teaching | 5 | Self-explaining interface |
| 5 | Emotional Polish & Trust | 5 | Professional finish |

**Total: 25 commits**
