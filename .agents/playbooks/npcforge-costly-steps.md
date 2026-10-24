---
extends: feature
when: Any NpcForge feature that reaches a step costly to redo, such as a paid model run, a recording, or a change to saved data.
---

# NpcForge: review before a step that costs money to redo

In this repo the costly step is usually a paid run against a real model, and the worst kind is a
recording that later tests replay. Once it is made, every fix to what it captured means paying for
it again. The Safety net epic showed the pattern: the design was decided, so Feature's own rule ran
no review. The review that did run before the recording found a flag slip that made a paid call,
and a recording format that left out most of what the app sends.

- **Replace** "If the design is contested, `interrogate` before shipping." with: If the design is contested, `interrogate` before shipping. Also run it before any step that is costly to redo, such as a paid run, a recording or a migration, even when the design is decided. First run the whole pipeline end to end with a scripted stand-in that produces the same shape, break-it checks included. Then review, and name the costly step in the intent, so that findings which change what is captured or sent land before it.
- **After** "Commit liberally." add: When the delegate exercises code that guards a costly side effect, such as a paid call or a write to real data, the brief puts a second, independent stop behind it that fails for free, such as an input refused before the cost or a dead network route. The brief then requires the dangerous combinations to be run. A brief that allows only inputs safe by construction never exercises the guard.
- **After** "Verify on the matching surface." add: Read **principle-prove-it-works** before this step. A check whose input never reached the path it guards is inconclusive: a fixture missing the case it protects, a process cut off before its side effect, a break-it edit that never compiled. So is a Done-when condition, such as "with no network", that was inferred from the design instead of imposed while the check ran.
