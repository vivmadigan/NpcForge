# A fixed wall or a scroll box makes the contrast check fail on text it cannot judge

- **Status:** active
- **Date:** 2026-11-15
- **Applies to:** `NpcForge.Web/src/app.css`, `NpcForge.Web/src/Frame.tsx`, `NpcForge.Web/src/Sheet.tsx`, `NpcForge.Web/tests/contrast.ts` · tags: web, css, contrast, axe, layout

## Symptom

The forge page's dungeon wall first sat behind the frame with `position: fixed`. The named-state
tests then failed on pages longer than the window, with no colour changed:

```
could not judge: p:nth-child(6) <p>
    Element's background color could not be determined because it partially overlaps other elements
```

The test counts a result axe cannot judge as a failure (ADR-017, decision 7).

## What did not work

- **A fixed wall.** Text that crosses the bottom edge of the window overlaps the wall's box, and axe
  gives up on its background. The agent that built the page saw nine tests fail this way.
- **A side column that sticks, with the brief scrolling inside it.** This was tried so that save,
  roll again and leave would stay in view beside a long sheet. The agent that built the page
  reported that axe then calls each brief line scrolled out of the box "partially obscured". This
  was not rerun for this lesson.
- **A side column that sticks as one block.** It passes the check, but the column is taller than
  the window. The agent that built the page measured save in view for the first 700 pixels of a
  1,402 pixel scroll at desktop width.

## What worked

The wall is `position: absolute` on a body that is `position: relative` with `min-height: 100vh`,
so it is the height of the page and nothing overlaps it part way. The keep actions sit at the top
of the side column and do not follow the scroll.

## Why it works here

axe works out a text's background from the boxes stacked under it. A box that covers only part of
the text, or a scroll box that clips it, leaves axe unable to say what colour is behind the text. The
pages keep text on one flat colour so the check can always judge it (ADR-017, Consequences).

## How to tell it is happening again

A contrast failure that starts with `could not judge` and names no colours. A new `position: fixed`
or `position: sticky`, or a new `overflow` that scrolls, in `app.css`. A failure on a page longer
than the window, such as an open save on a phone.
