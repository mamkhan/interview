---
name: real-work
description: Use when planning a multi-step task or working in plan mode and you need to capture the plan as a durable, resumable artifact — breaking work into phases with per-item checkboxes, completion tracking, autonomous verification, and a handoff summary so a future agent can pick up where you left off. Use when user wants to create or design a plan or mentioned "real work".
---

# Real Work

Turn planning into a durable, resumable artifact. The plan file — not the conversation — is the source of truth: it records what to do, what's done, and how it was verified. Any future agent can resume from it with zero prior context.

**Use when** planning multi-step / multi-session work that may outlive the current session. Skip for trivial single-session tasks.

## General Instructions

Follow these instructions unless the user asks for a different approach:

- **The work is NOT done until the real app has been driven end-to-end by hand and the real output observed.** Automated tests passing is necessary but never sufficient — see §3 and the mandatory final validation phase.


## 1. Reach complete understanding first

Do **not** write the plan until scope is fully understood. Relentlessly ask the user questions until you both share a complete understanding with **no gaps** — treat an unasked question as a future bug.

### Business level first, technical detail second

**Settle the business direction before raising any technical option.** A plan built on a technical choice
the user could not evaluate is a plan built on a guess.

For each problem the plan will address, present in this order:

1. **What each side actually does, in the reader's world.** Before any comparison, give each system,
   version or option its own short paragraph: what it does, for whom, and why it produces the result it
   produces. Describe behaviour, never code. No class names, file names, column names, proc names or line
   numbers in this part — those go in a later, clearly marked technical section.
2. **Impact in the user's terms.** What breaks, for how many customers, meters or records, how often, and
   whether it self-corrects. Money, volume, time, risk. Quantify in units the reader already uses
   (kWh a day, euros, customers), not ratios without a baseline.
3. **Which option is closest to right, including "neither".** State it plainly and show the numbers side by
   side. If you have not established that one option is more accurate, say so — do not let a recommendation
   carry an accuracy claim you cannot evidence. "Ours is better" when you have only shown "ours is
   different" is the single most expensive sentence in a plan.
4. **The weakness of your own recommendation.** One sentence on what is unsatisfying about it and what
   evidence would change it.
5. **Only questions a product owner could answer** without opening the repository: deadlines, tolerances,
   priorities, what is acceptable to lose, what must never be lost, which environment matters. If answering
   a question requires reading the code, it is yours to answer, not theirs.

Do **not** ask the user to choose between technical designs. Retry strategies, atomicity mechanisms, schema
changes, library choices, test structure and tooling are engineering decisions. Decide them, then state what
you chose and what evidence would change it. Naming three implementation options and asking "which do you
prefer?" is a failure of this step, not thoroughness.

**If the user says they are confused, your explanation was at fault.** Do not repeat it with more detail or
more evidence — that is what caused the confusion. Rewrite from their vocabulary, starting from what the
system does for a customer, and say plainly which of your earlier sentences was misleading and what it
should have said. Repeated rounds of clarification are a signal that step 1 was skipped, not that the
subject is hard.

Once the business direction is confirmed, discuss technical detail freely and in full precision, and record
those decisions in the plan.

**Never break a working case to fix a broken one.** Before a behaviour change is planned, identify who else
is on that code path today and what they currently produce. Measure it against the real population where
one is reachable. Any case that is correct today must stay correct: if the change would alter it, the plan
must either preserve the old behaviour for that case explicitly, or record why the change is safe with the
measurement that shows it. A plan that fixes a cohort and silently moves everyone else is not a fix.

- Don't stop at the first round; keep going until no ambiguity, assumption, or open decision remains. Probe edges: scope boundaries (in/out), dependencies, constraints, success criteria, data, environments, deployment, failure cases.
- Surface every assumption for the user to confirm. If an answer opens a new unknown, ask the follow-up — drill down recursively.
- Do not use `AskUserQuestion` for user inputs. When required, summarize the full scope back and only proceed once the user confirms nothing is missing. Provide context for the questions to allow the user to make informed decisions. The user needs to be informed about the business implications of technical decisions.


## 2. Write the plan

Save to `.ai-scratch/<descriptive-name>.md` in the **repository root** (create `.ai-scratch/` if needed; it is gitignored). Use this self-documenting template:

```markdown
# <Work Title>

<1-2 sentence goal and scope.>

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary** (what was done, key
decisions, anything needed to continue with zero context); run the phase's
**Verification Plan** and record the result before moving on. The work is only
"done" when the final **Manual end-to-end validation** phase has been performed
against the real running app with observed-output evidence recorded. When all
phases are done, fill in **Final Recap** and **Deployment Plan**.

## Phase 1: <Title>
Status: Not started   <!-- Not started | In progress | Complete -->

- [ ] <concrete, actionable item>
  Technical details of the item if required
- [ ] <concrete, actionable item>
  Technical details of the item if required

### Verification Plan
- <command/check the agent can run autonomously, with expected result>

### Phase Summary
_(write when phase completes)_

## Phase 2: <Title>
Status: Not started

- [ ] <actionable item>
  Technical details of the item if required

### Verification Plan
- <autonomous check>

### Phase Summary
_(write when phase completes)_

## Phase N (final): Manual end-to-end validation  — MANDATORY GATE
Status: Not started

This phase CANNOT be satisfied by automated tests, builds, or resource health alone.
- [ ] Start the app the way a user/operator runs it (e.g. `aspire run`), not a test harness.
- [ ] Drive a REAL input end-to-end (drop the real file, call the real endpoint, click the real button).
- [ ] Observe the REAL output at EVERY hop — query the DB, list the blobs/files, read the
      emitted events / logs / UI. Confirm the values are actually correct, not just present.
- [ ] Paste the concrete observed evidence into the Phase Summary (row values, file names,
      event payloads, screenshots). "All resources Running/Healthy" is NOT evidence.
- [ ] If anything is wrong, the work is NOT done: fix it, and if green tests missed it,
      fix the test suite too (see §3 test-fidelity rule).

### Verification Plan
- Record the actual observed outputs end-to-end, tied to the known input. The check is the
  real data the system produced — not process status, not test counts.

### Phase Summary
_(write when phase completes — include the observed-output evidence)_

## Final Recap
_(write when all phases complete: summary of the entire piece of work)_


## 3. Verify each phase, then validate the whole system

**Liveness ≠ correctness.** A process that is "Running"/"Healthy", a build that succeeds, and a
green test suite are all liveness signals. None of them prove the feature does the right thing.
Correctness is only established by feeding a known input into the *real* system and observing the
*real* output. Never report "working" / "everything passing" off the back of health or test status.

- **Per phase:** give an autonomous **Verification Plan** (runnable command + expected output) so a
  future agent can re-check without you. This is for fast regression confidence, not final proof.
- **After all phases:** perform the **Manual end-to-end validation** gate (the final template phase).
  Use the aspire skills (or however the app actually runs) to start the system, drive a real input,
  and read the real output at each hop. Only then is the work validated.

**Test-fidelity rule (why green tests can hide a broken app).** Integration tests MUST exercise the
same configuration and wiring the real app runs with:

- Tests must use the **real composition root** and the **real default values** (subscription names,
  connection-string keys, topic/queue names, container names, ports). If a test *overrides* the exact
  config the app relies on by default, it stops testing the app and starts testing a parallel
  universe — it will stay green while the wired app fails (e.g. a consumer whose default subscription
  name doesn't match the one the AppHost declares).
- Prefer at least one test that boots the **real wiring** (e.g. the Aspire AppHost via
  `DistributedApplicationTestingBuilder`, or the production composition root) so config/wiring drift
  is caught automatically.
- Keep test-only fixtures (e.g. hand-written emulator configs, inline connection settings) in sync
  with the real wiring, or derive them from the same source. Divergence here is a latent bug.
- **When the tests are green but the app is broken, suspect this divergence first.** Reproduce against
  the real app, then add/repair a test that exercises the real wiring so the bug can't recur.

## Common mistakes

- **Vague items** — each checkbox is a concrete task ("Add retry logic to `PaymentClient.Charge`"),
  not a theme ("improve payments").
- **Mistaking liveness for correctness** — reporting "working" because resources are Running/Healthy,
  the build passed, or tests are green, without driving real input and observing real output.
- **Skipping the manual end-to-end gate** — the final validation phase is mandatory and must record
  observed-output evidence, not status.
- **Tests that diverge from app config** — overriding the defaults/wiring the real app uses, so the
  suite passes while the app fails. At least one test must exercise the real composition.
- **Per-phase verification that isn't autonomous** — per-phase checks should be runnable commands with
  expected output (the final-phase manual validation is the deliberate exception, and it requires
  recorded evidence).
- **Wrong location** — always the repo-root `.ai-scratch/` folder.
- **Pre-filling summaries** — phase summaries, recap, and deployment plan stay as placeholders until
  that work actually completes (and the manual gate has been performed).
```

