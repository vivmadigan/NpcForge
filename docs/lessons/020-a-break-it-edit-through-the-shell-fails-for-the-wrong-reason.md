# A break-it edit made through shell quoting goes red for the wrong reason

- **Status:** active
- **Date:** 2026-11-15
- **Applies to:** break-it checks, `NpcForge.Web/src/**`, any temporary edit made with `sed` or a heredoc · tags: tests, break-it, shell, quoting

## Symptom

In epic 12, a break-it check that should have failed with a list of missing words failed like this:

```
Test timeout of 30000ms exceeded.
Error: locator.click: Test timeout of 30000ms exceeded.
  - waiting for getByRole('link', { name: /^13 / })
```

The edit was a `sed` that put `sheet.split('\n\n')[0]` into `Sheet.tsx`. The shell turned each `\n`
into a real line break inside the string, the file no longer compiled, and the page never drew. The
test was red, and the guard it was meant to prove had not run.

## What did not work

- Taking red as the proof. A timeout, a syntax error and the predicted mismatch all print red.
- More escaping in the same `sed`. The agent that built the forge page hit the same thing twice
  more, once as a syntax error and once as a file that did not compile, and a CSS insert through a
  heredoc that changed nothing at all.

## What worked

Make the temporary edit with the editor tool or a script file, never through nested shell quotes.
Print `git diff --stat` straight after, to see that one line changed. Then read the failure for the
disagreement that was predicted, such as `- Expected - 553` with the missing words listed, before
counting it. Restore with `git checkout -- <file>` and run the suite again.

A `sed` that swaps one plain token for another, such as one colour for another, has been safe.

## Why it works here

A break-it check proves a guard only when the code still runs and the guard is what fails. Vite
serves a file that does not compile as an error page, so every test that opens the page times out,
whatever it guards.

## How to tell it is happening again

A break-it failure that is a timeout, or that names an element the edit did not touch. A
`git diff` after the edit that shows a broken line, or no change. See also lesson 016, where a
restored file left the break in the build.
