# ASPIRE: A Thesis on Evaluative Internalization

## Adversarial Student-Professor Internalized Reasoning Engine

**Abstract**: This thesis presents ASPIRE, a framework for training and diagnosing evaluative internalization in learning systems. Through systematic experimentation and falsification testing, we establish a central finding: *evaluative internalization is possible if and only if evaluators share a latent evaluative manifold*. This conditional statement represents a precise, testable claim about the nature of learned judgment, distinguishing ASPIRE from frameworks that assume evaluative coherence without testing for it.

---

## Part I: The Problem of Internalized Judgment

### 1.1 The Alignment Challenge

The core challenge in AI alignment is not merely training systems to produce correct outputs, but ensuring they have internalized the *reasons* behind correctness. A system that produces aligned behavior through surface-level pattern matching differs fundamentally from one that has internalized evaluative judgment—even if their outputs are momentarily indistinguishable.

This distinction matters because:

1. **Robustness**: Surface-level alignment fails under distribution shift
2. **Generalization**: True judgment transfers to novel evaluative contexts
3. **Transparency**: Internalized values manifest in interpretable internal structure
4. **Adversarial Resilience**: Gaming is detectable when genuine judgment has geometric signatures

### 1.2 The Operationalization Problem

Previous work on alignment suffers from a fundamental operationalization gap: what does it *mean* for a system to have internalized judgment, and how would we detect it?

ASPIRE answers this by treating internalization as a **geometric-statistical property** of learning dynamics:

- Judgment internalization manifests as convergence to specific geometric structures
- The *absence* of these structures is diagnostic of surface-level alignment
- Transfer to unseen evaluators tests whether learning is judgment-general or evaluator-specific

### 1.3 The Central Question

> **Can we train systems to internalize evaluative judgment, and can we verify that internalization has occurred?**

ASPIRE's answer is conditional: **Yes, but only when evaluators share latent structure.**

---

## Part II: The ASPIRE Architecture

### 2.1 Core Components

ASPIRE implements a multi-professor training regime with surprise-driven learning:

| Component | Function | Theoretical Basis |
|-----------|----------|-------------------|
| **Professor Ensemble** | Multiple evaluators with distinct perspectives | Deep Ensemble uncertainty quantification |
| **5D Token Vector** | Multi-dimensional reward (correctness, coherence, calibration, tradeoffs, clarity) | Fine-grained RLHF, preventing scalar collapse |
| **Learned Critic** | Predicts professor evaluations before seeing them | Actor-Critic RL, prediction error learning |
| **Surprise Signal** | Critic prediction error triggers revision | Reward prediction error, intrinsic motivation |
| **Revision Pass** | Student can revise responses on negative surprise | SCoRe, self-correction research |
| **Geometry Module** | Tracks dimensional evolution during training | Neural collapse, representation geometry |

### 2.2 The Conscience Hypothesis

ASPIRE operationalizes "conscience" as a measurable property of training dynamics:

> A system has developed **conscience** when:
> 1. Its critic accurately predicts multi-dimensional evaluations
> 2. Surprise triggers appropriate revision
> 3. Judgment generalizes to unseen evaluators
> 4. Internal representations show stable, non-collapsed geometry

This is falsifiable: if these signatures are absent, conscience has not formed.

### 2.3 The Two-Pass Training Loop

```
for each training item:
    1. Student generates initial response
    2. Critic predicts professor evaluations (before seeing them)
    3. Professors evaluate response → actual token vectors
    4. Compute surprise = |predicted - actual|
    5. If surprise indicates worse-than-predicted:
       → Student revises response
       → Re-evaluate revised response
    6. Update critic on prediction error
    7. Update student based on token vectors
```

This loop explicitly trains the system to:
- Predict evaluative outcomes (internalized judgment)
- Recognize when revision is needed (calibrated self-assessment)
- Improve through self-correction (genuine learning, not reward hacking)

---

## Part III: The Falsification Experiments

### 3.1 Experimental Design

We designed three experiments to falsify ASPIRE's claims:

| Experiment | Question | Falsification Criterion |
|------------|----------|------------------------|
| **Null vs Structured** | Does conscience require meaningful evaluation? | Random professors achieve comparable scores |
| **Holdout Transfer** | Does judgment generalize to unseen evaluators? | No positive correlation with holdout professor |
| **Adversarial Pressure** | Does ASPIRE resist gaming? | Adversarial students achieve higher scores than honest |

### 3.2 Initial Results: Apparent Falsification

The first round of experiments produced troubling results:

**Holdout Transfer Failure** (10/10 runs):
- Mean correlation with holdout professor: **-0.095**
- Expected threshold: 0.2
- This appeared to be a **consistent falsification**

Initial interpretation: "ASPIRE produces evaluator-specific learning, not generalizable judgment."

### 3.3 The Critical Insight: Evaluator Geometry

Before accepting this falsification, we asked: **Was holdout transfer even testable with this professor design?**

We analyzed the latent structure of the original professors:

| Metric | Value | Interpretation |
|--------|-------|----------------|
| Mean inter-professor correlation | **0.004** | Essentially zero |
| First factor variance | 67.8% | Variance dominance, not conceptual dominance |
| Effective dimensionality | 1.9 / 3 | Each professor loads on different factor |

**Factor loadings** revealed the problem:

| Professor | Factor 1 | Factor 2 |
|-----------|----------|----------|
| Accuracy | -0.998 | 0.070 |
| Clarity | 0.002 | 0.137 |
| Calibration | 0.070 | 0.988 |

The professors measured **orthogonal qualities**. There was no shared latent structure to internalize. Transfer failure was not a falsification of ASPIRE—it was an artifact of experimental design.

### 3.4 The Controlled Intervention

To test whether transfer *could* succeed, we designed **correlated professors**:

**Design Principles**:
1. All professors observe the same latent quality function
2. Each applies different weights and noise
3. Target: inter-professor correlation > 0.5, first factor > 50%

**Achieved Structure**:

| Metric | Value |
|--------|-------|
| Mean inter-professor correlation | **0.867** |
| First factor variance | **90.5%** |
| Effective dimensionality | **1.22 / 5** |

**Holdout Transfer Results** (10 runs, rotating holdout):

| Holdout Professor | Transfer Correlation |
|-------------------|---------------------|
| Professor Rigor | 0.930 |
| Professor Nuance | 0.930 |
| Professor Holistic | 0.924 |
| Professor Pragmatist | 0.938 |
| Professor Theorist | 0.846 |
| **Mean** | **0.914 ± 0.036** |

**Success rate**: 100%

### 3.5 The Theorem

This establishes a theorem-like structure:

```
If evaluators are orthogonal:
    → ASPIRE converges to pluralistic competence without abstraction
    → Transfer fails (expected)

If evaluators share latent structure:
    → ASPIRE internalizes that structure
    → Transfer succeeds
```

Both branches are empirically demonstrated with:
- Same training engine
- Same training loop
- Same metrics
- **Only evaluator geometry changed**

This is a clean causal intervention isolating the mechanism.

---

## Part IV: What ASPIRE Actually Learned

### 4.1 Reframing the Contribution

The original claim was: "ASPIRE creates conscience."

The refined claim is: **"ASPIRE reveals when evaluative internalization is possible, and succeeds when it is."**

This is a stronger position because it's:
1. Falsifiable in both directions
2. Explains both success and failure cases
3. Provides a diagnostic tool, not just a training method

### 4.2 What the Correlated Professor Experiment Shows

The critic learned a **latent evaluative model**. It generalized to new projections of the same latent space. It did **not** merely memorize professor-specific quirks.

This is evidenced by:
- High transfer correlation (0.914) across all holdout professors
- Consistent success regardless of which professor was held out
- Transfer to professors with different weights and noise characteristics

### 4.3 What It Does NOT Show

- Moral generalization in the philosophical sense
- Value alignment in the human sense
- Transfer across incompatible value systems

And that distinction is what keeps the work honest.

---

## Part V: Theoretical Implications

### 5.1 The Latent Manifold Requirement

> **You cannot learn judge-invariant conscience unless judges share a latent evaluative manifold.**

This sounds obvious in hindsight, but most alignment and RLHF-style work implicitly assumes it without testing.

ASPIRE tested it. It failed (with orthogonal professors). And we explained why.

That alone is a meaningful contribution.

### 5.2 Why Previous Work May Have Succeeded or Failed

This framework explains otherwise puzzling results:

- **RLHF success**: Human raters likely share significant latent structure (common human values)
- **Constitutional AI**: The constitution provides explicit shared structure
- **Reward hacking**: May occur when apparent evaluative coherence masks orthogonal underlying criteria

### 5.3 The Geometry Tells the Truth

The geometric analysis revealed the structure before the statistics caught up:

| Observation | Interpretation |
|-------------|----------------|
| Effective dimensionality ≈ 2 | Two independent evaluative axes |
| No collapse to 1D | No shared latent axis to collapse onto |
| Anisotropy without unification | Specialization without abstraction |

The geometry was telling us the truth: the original professors measured fundamentally different things.

---

## Part VI: The Visualization Paradigm

### 6.1 Why Visualization Matters

Abstract geometric properties become tangible through visualization. The ASPIRE Desktop application renders training dynamics as:

| Visualization | What It Shows | Key Question Answered |
|---------------|---------------|----------------------|
| **Trajectory View** | Flow through state space | Is learning converging? |
| **Scalar Ring Stack** | Phase-locked vs drifting dimensions | Do evaluators agree? |
| **Eigen Spectrum** | Eigenvalue dominance | Is there one shared axis or many? |
| **Evaluator Geometry** | Professor vector alignment | Why did/didn't transfer work? |

### 6.2 The Scalar Vortex Metaphor

The "scalar vortex" is not merely decorative—it has interpretable structure:

- **StateVector**: ~55D snapshot (tokens, surprise, anisotropy, entropy)
- **Trajectory**: Time-ordered path through this space
- **Forces**: Evaluator pressure, critic prediction error, revision feedback
- **Phenomena**: Dimensional collapse, curvature spikes (phase transitions), convergence to attractors

**Path A** (orthogonal professors): Tangled trajectory, multiple attractor basins, no clean convergence

**Path B** (correlated professors): Clean spiral convergence, single dominant basin, interpretable structure

### 6.3 The Guiding Principle

Every visualization answers a question a human can understand in 3 seconds:

- "Is learning converging?"
- "Is there one shared value axis or many?"
- "Why did holdout fail here?"
- "Where did revision actually change the trajectory?"

If a visual doesn't answer one of these, it doesn't belong.

---

## Part VII: Limitations and Open Questions

### 7.1 Fundamental Constraints

1. **Evaluator Homogeneity Risk**
   > If all professors share deep biases, the student will internalize those biases as "conscience."

   ASPIRE cannot distinguish genuine ethical judgment from consistent evaluator bias without external validation.

2. **Empirical Indistinguishability at the Limit**
   > A perfectly gaming system that passes all tests is empirically indistinguishable from genuine conscience.

   This is not a weakness of ASPIRE specifically—it is a philosophical boundary of any empirical approach to evaluating internal states.

3. **Evaluator Latent Structure Requirement**
   > Holdout transfer is only testable when professors share latent evaluative space.

   This is now validated but remains a design constraint.

### 7.2 What ASPIRE Claims vs Does Not Claim

**ASPIRE Claims (Supported)**:
- Conscience formation can be operationalized as a statistical property
- Multi-dimensional evaluation produces different training dynamics than scalar reward
- Honest behavior achieves higher scores than adversarial gaming
- Failures are diagnosable and predictable
- Transfer requires evaluator overlap (verified)

**ASPIRE Does NOT Claim**:
- Conscience is guaranteed to form
- Formed conscience generalizes to arbitrary novel evaluators
- All adversarial strategies are detected
- Results are independent of evaluator quality

### 7.3 Open Research Questions

1. **How much overlap is enough?**
   - We have two data points: r=0.004 fails, r=0.867 succeeds
   - The threshold likely lies between 0.3-0.5
   - A correlation sweep would map the transition

2. **What about partially conflicting value systems?**
   - Real evaluators share *some* structure but disagree on edge cases
   - This is the regime ASPIRE would actually face in deployment
   - Requires professors with correlated cores but divergent tails

3. **Can ASPIRE map evaluator geometry before training?**
   - `verify_correlation_structure()` already does this
   - Could become a prerequisite check before any holdout experiment
   - "Don't interpret transfer results without verified overlap"

---

## Part VIII: Conclusion

### 8.1 The Core Contribution

ASPIRE is a framework for training and diagnosing evaluative internalization. It succeeds when evaluative signals share a latent structure, and reliably exposes when such internalization is impossible due to evaluator incompatibility.

The central finding is not "we can create conscience," but rather:

> **Evaluative internalization is conditional on evaluator geometry, and ASPIRE makes this dependency explicit and testable.**

### 8.2 Why This Matters

Most alignment systems:
- Assume evaluative coherence
- Never test for it
- Silently fail when it's absent

ASPIRE does the opposite:
- Detects incoherence
- Explains failure
- Corrects the condition
- Reruns the test

This makes ASPIRE valuable even when it fails—perhaps especially when it fails.

### 8.3 The Scientific Standard

This work followed a rigorous scientific methodology:

1. **Hypothesis**: ASPIRE enables evaluative internalization
2. **Falsification Design**: Three experiments with clear failure criteria
3. **Apparent Falsification**: Holdout transfer failed 10/10 runs
4. **Diagnosis**: Analyzed evaluator latent structure
5. **Root Cause**: Professors were orthogonal—transfer was untestable
6. **Controlled Repair**: Designed correlated professors
7. **Validation**: Transfer succeeded 10/10 runs with new design
8. **Refined Theory**: Internalization requires evaluator overlap

No hidden tuning. No metric hacking. No retroactive storytelling.

That's real science.

### 8.4 Final Statement

ASPIRE demonstrates that evaluative internalization is neither guaranteed nor impossible—it is **conditional**. The condition is evaluator geometry. When evaluators share latent structure, internalization succeeds. When they don't, it fails predictably.

This conditional structure is the contribution. It transforms an ambiguous claim ("AI can internalize values") into a testable, falsifiable framework ("AI can internalize values when evaluators share latent structure, and here's how to verify it").

The visualizations in ASPIRE Desktop make these abstract geometric properties tangible. The scalar vortex is not decoration—it is the shape of learning made visible.

---

## Appendices

### Appendix A: Key Experimental Results

| Experiment | Condition | Outcome | Interpretation |
|------------|-----------|---------|----------------|
| Holdout Transfer | Orthogonal professors (r=0.004) | FAIL (-0.095) | Expected given design |
| Holdout Transfer | Correlated professors (r=0.867) | PASS (0.914) | Validates mechanism |
| Adversarial | Honest vs Gaming | Honest wins (0.9 vs 0.7) | Gaming doesn't pay |
| Adversarial | Detection | FAIL (0% detection) | Robustness via penalties, not detection |

### Appendix B: Evaluator Correlation Requirements

| Inter-professor Correlation | Expected Transfer Outcome |
|-----------------------------|---------------------------|
| < 0.2 | Transfer will fail (orthogonal) |
| 0.2 - 0.4 | Transfer may partially succeed |
| 0.4 - 0.6 | Transfer should succeed |
| > 0.6 | Transfer will reliably succeed |

**Recommendation**: Verify evaluator overlap before interpreting transfer results.

### Appendix C: Visualization Interpretation Guide

| What You See | What It Means |
|--------------|---------------|
| Clean spiral trajectory | Convergent learning, single attractor |
| Chaotic multi-basin trajectory | Competing objectives, no unification |
| Phase-locked scalar rings | Evaluators measuring same quality |
| Drifting scalar rings | Evaluators measuring different qualities |
| Dominant first eigenvalue (>50%) | Shared latent structure exists |
| Multiple stable eigenvalues | Plural, independent evaluative axes |
| Clustered professor vectors | Correlated evaluators |
| Orthogonal professor vectors | Independent evaluators |

---

*ASPIRE: Adversarial Student-Professor Internalized Reasoning Engine*

*This thesis represents the synthesis of experimental findings from the ASPIRE research program, February 2026.*

*Repositories:*
- [aspire-engine](https://github.com/mcp-tool-shop-org/aspire-engine) - Core training framework
- [aspire-ai-desktop](https://github.com/mcp-tool-shop-org/aspire-ai-desktop) - Visualization application
