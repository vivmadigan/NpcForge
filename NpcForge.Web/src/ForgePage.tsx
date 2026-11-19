import { useEffect, useRef, useState, type ReactElement } from 'react';
import {
  answer,
  ApiError,
  rollAndWrite,
  save,
  startInterview,
  type ConfirmEntry,
  type InterviewId,
  type InterviewStep,
  type RunResult,
} from './api';
import { Confirmation } from './Confirmation';
import { Frame } from './Frame';
import { SceneForm } from './SceneForm';
import { Sheet } from './Sheet';
import { Title } from './Title';
import { Wait } from './Wait';

type Written = Extract<RunResult, { kind: 'written' }>;
type Kept = { interview: InterviewId; character: Written };

// A step the GM can read and act on.
type Settled =
  | { kind: 'describe'; scene: string }
  | { kind: 'turned-down'; scene: string } // nothing was rolled
  | { kind: 'left'; scene: string } // nothing was saved
  | { kind: 'question'; scene: string; interview: InterviewId; field: string; question: string; answer: string }
  | { kind: 'confirm'; scene: string; interview: InterviewId; entries: ConfirmEntry[] }
  | { kind: 'written'; scene: string; interview: InterviewId; character: Written }
  | { kind: 'no-name'; scene: string; interview: InterviewId; reason: string }
  | { kind: 'stopped'; scene: string; reason: string } // the spending cap
  // The API's fault, as it sent it (ADR-019 decision 4). `written` is the character a failed save
  // was keeping. A new roll costs money and a repeated save is safe (decision 3), so the page keeps
  // it and offers the save again.
  | { kind: 'failed'; scene: string; reason: string; written?: Kept }
  | { kind: 'saved'; scene: string; interview: InterviewId; number: number };

// A call to the seam is out. `from` is the step the GM just left, drawn under the fade (ADR-018
// decision 8). A state opened by name has none, so it shows black at once.
type Waiting =
  | { kind: 'reading'; scene: string; from?: Settled } // waiting on the interviewer
  | { kind: 'writing'; scene: string; interview: InterviewId; from?: Settled };

// The page holds one of these and draws from it with one switch (principle-model-the-domain). Each
// is plain data, so a state can be opened by name (ADR-017 decision 6). Every step after the first
// carries the scene, which stays on screen above it. Changing the scene after the confirmation and
// leaving are not seam calls (ADR-017 decision 3); they put the scene back in the box.
export type ForgeState = Settled | Waiting;

function isWaiting(state: ForgeState): state is Waiting {
  return state.kind === 'reading' || state.kind === 'writing';
}

function afterInterview(scene: string, step: InterviewStep): ForgeState {
  switch (step.kind) {
    case 'question':
      return { ...step, scene, answer: '' };
    case 'confirm':
      return { ...step, scene };
    case 'stopped':
      return { kind: 'stopped', scene, reason: step.reason };
  }
}

function afterRoll(scene: string, interview: InterviewId, result: RunResult): ForgeState {
  switch (result.kind) {
    case 'written':
      return { kind: 'written', scene, interview, character: result };
    case 'no-name':
      return { kind: 'no-name', scene, interview, reason: result.reason };
    case 'stopped':
      return { kind: 'stopped', scene, reason: result.reason };
  }
}

// `saved` is the header's count and `recount` asks for it again after a save. `start` opens a named
// state without calling the seam.
export function ForgePage({
  saved,
  recount,
  start,
}: {
  saved: number | null;
  recount: () => void;
  start?: ForgeState;
}) {
  const [state, setState] = useState<ForgeState>(start ?? { kind: 'describe', scene: '' });

  // A new step replaces the last one, so it starts at the top, and focus moves to it from the button
  // that has gone. Typing keeps the step, so only a change of step does this. A wait is not a step:
  // the old one is still drawn under the fade, so it stays where it is.
  const step = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (isWaiting(state)) return;
    window.scrollTo(0, 0);
    step.current?.focus({ preventScroll: true });
  }, [state.kind]);

  // The step on screen when a wait begins, to fade out from.
  const leaving = (): Settled | undefined => (isWaiting(state) ? state.from : state);

  // A call to the seam, and the step it leads to. A fault from the API ends it in the failure state
  // with the scene kept, whatever step it came from (ADR-019 decision 4), and with the character
  // when the call was its save. Anything else thrown is a bug, and is left to surface.
  async function settle(scene: string, next: () => Promise<ForgeState>, written?: Kept) {
    try {
      setState(await next());
    } catch (fault) {
      if (!(fault instanceof ApiError)) throw fault;
      setState({ kind: 'failed', scene, reason: fault.message, written });
    }
  }

  // Each wait ends when the seam answers and not before: the page sets no timer (the epic's
  // constraint, ADR-018 decision 8).
  async function send(scene: string) {
    setState({ kind: 'reading', scene, from: leaving() });
    await settle(scene, async () => afterInterview(scene, await startInterview(scene)));
  }
  async function reply(scene: string, interview: InterviewId, text: string) {
    setState({ kind: 'reading', scene, from: leaving() });
    await settle(scene, async () => afterInterview(scene, await answer(interview, text)));
  }
  async function roll(scene: string, interview: InterviewId) {
    setState({ kind: 'writing', scene, interview, from: leaving() });
    await settle(scene, async () => afterRoll(scene, interview, await rollAndWrite(interview)));
  }
  async function keep(scene: string, interview: InterviewId, character: Written) {
    await settle(
      scene,
      async () => {
        const number = await save(character.draft);
        recount();
        return { kind: 'saved', scene, interview, number };
      },
      { interview, character },
    );
  }

  // The return type has no `undefined` in it, so a step with no case here fails tsc and cannot
  // ship as a blank page (ADR-017, "Outcomes as values").
  function draw(s: Settled): ReactElement {
    switch (s.kind) {
      case 'describe':
      case 'turned-down':
      case 'left': {
        return (
          <SceneForm
            scene={s.scene}
            after={s.kind === 'describe' ? null : s.kind}
            onScene={(scene) => setState({ ...s, scene })}
            onSend={send}
          />
        );
      }
      case 'question': {
        const { scene, interview, answer: text } = s;
        return (
          <form
            onSubmit={(event) => {
              event.preventDefault();
              if (text.trim() !== '') reply(scene, interview, text.trim());
            }}
          >
            <ScenePanel scene={scene} />
            <h1 className="page-title">ONE QUESTION</h1>
            <p>Your scene does not say this, and the dice cannot pick it:</p>
            <label className="question" htmlFor="answer">
              <span className="field-name">{s.field}</span> {s.question}
            </label>
            <input
              id="answer"
              className="answer-box"
              value={text}
              onChange={(event) => setState({ ...s, answer: event.target.value })}
            />
            <button className="button button-main" type="submit" disabled={text.trim() === ''}>
              ANSWER
            </button>
          </form>
        );
      }
      case 'confirm': {
        const { scene, interview } = s;
        return (
          <>
            <ScenePanel scene={scene} look="large" />
            <Confirmation entries={s.entries} />
            <p className="ask-big">Shall I roll this character?</p>
            <div className="actions">
              <button className="button button-main" type="button" onClick={() => roll(scene, interview)}>
                ROLL IT
              </button>
              <button className="button" type="button" onClick={() => setState({ kind: 'turned-down', scene })}>
                CHANGE THE SCENE
              </button>
            </div>
          </>
        );
      }
      case 'written': {
        const { scene, interview, character } = s;
        // The same three buttons sit above the sheet and below it, as the sketch draws them. The
        // top set is in view when the page opens, so the GM never scrolls to answer.
        const buttons = (
          <div className="actions">
            <button className="button button-main" type="button" onClick={() => keep(scene, interview, character)}>
              SAVE
            </button>
            <button className="button" type="button" onClick={() => roll(scene, interview)}>
              ROLL AGAIN
            </button>
            <button className="button" type="button" onClick={() => setState({ kind: 'left', scene })}>
              LEAVE
            </button>
          </div>
        );
        return (
          <>
            <div className="panel keepbar">
              <ScenePanel scene={scene} look="small" />
              <section aria-label="Keep, roll again or leave">
                <p className="ask">Keep this character?</p>
                {buttons}
                <p className="hint">Roll again: new dice, same scene.</p>
              </section>
            </div>
            <Sheet name={character.name} sheet={character.sheet} />
            <section className="panel foot" aria-label="Keep, roll again or leave, below the sheet">
              <p className="ask">Keep this character?</p>
              {buttons}
            </section>
          </>
        );
      }
      case 'no-name': {
        const { scene, interview } = s;
        return (
          <>
            <ScenePanel scene={scene} />
            <h1 className="page-title">NO NAME</h1>
            <p className="notice">Not written: {s.reason}</p>
            <p>The dice were rolled, but no name followed them, so the character was not written. Nothing was saved.</p>
            <div className="actions">
              <button className="button button-main" type="button" onClick={() => roll(scene, interview)}>
                ROLL AGAIN
              </button>
              <button className="button" type="button" onClick={() => setState({ kind: 'describe', scene })}>
                CHANGE THE SCENE
              </button>
            </div>
          </>
        );
      }
      case 'stopped':
        return (
          <>
            <ScenePanel scene={s.scene} />
            <h1 className="page-title">STOPPED</h1>
            <p className="notice">Stopped: {s.reason} Nothing was saved.</p>
            <p>Your saved characters are still there.</p>
            <div className="actions">
              <a className="button" href="/saved">
                OPEN THE SAVED CHARACTERS
              </a>
            </div>
          </>
        );
      case 'failed': {
        const { scene, written } = s;
        const back = (
          <button
            className={written ? 'button' : 'button button-main'}
            type="button"
            onClick={() => setState({ kind: 'describe', scene })}
          >
            BACK TO YOUR SCENE
          </button>
        );
        // After a failed save the page names the character and does not say "nothing was saved": an
        // answer lost on the way back may hide a save that landed, and a second save of the same
        // draft returns the first one's number (ADR-019 decision 3).
        return (
          <>
            <ScenePanel scene={scene} />
            <h1 className="page-title">FAILED</h1>
            <p className="notice">Failed: {s.reason}</p>
            {written ? (
              <>
                <p>{written.character.name} is kept, so you can try the save again.</p>
                <div className="actions">
                  <button
                    className="button button-main"
                    type="button"
                    onClick={() => keep(scene, written.interview, written.character)}
                  >
                    TRY THE SAVE AGAIN
                  </button>
                  {back}
                </div>
              </>
            ) : (
              <>
                <p>Your scene is kept, so you can send it again.</p>
                <div className="actions">{back}</div>
              </>
            )}
          </>
        );
      }
      case 'saved': {
        const { scene, interview, number } = s;
        return (
          <>
            <ScenePanel scene={scene} />
            <h1 className="page-title">SAVED</h1>
            <p className="notice">Saved as {number}.</p>
            <div className="actions">
              <a className="button button-main" href={`/saved/${number}`}>
                OPEN SAVE {number}
              </a>
              <a className="button" href="/saved">
                OPEN THE SAVED LIST
              </a>
            </div>
            <h2 className="example-title">ANOTHER CHARACTER</h2>
            <div className="actions">
              <button className="button" type="button" onClick={() => roll(scene, interview)}>
                ROLL ANOTHER
              </button>
              <a className="button" href="/">
                NEW SCENE
              </a>
            </div>
            <p className="hint">Roll another: new dice, same scene.</p>
          </>
        );
      }
    }
  }

  // The first step, and the two that return to it, open on the big title. A written character is the
  // one step whose own pieces are panels, with the parchment between; every other step is one panel.
  function frame(s: Settled): ReactElement {
    const first = s.kind === 'describe' || s.kind === 'turned-down' || s.kind === 'left';
    const written = s.kind === 'written';
    return (
      <Frame saved={saved} here="forge" wide={written}>
        {first && (
          <>
            <Title />
            <p className="tagline">
              <span>Dice settle who they are. A model writes them.</span>
            </p>
          </>
        )}
        <div className={written ? 'step stack' : 'step'} ref={step} tabIndex={-1}>
          {written ? draw(s) : <div className="panel">{draw(s)}</div>}
        </div>
      </Frame>
    );
  }

  // During a wait the whole window is black, and nothing else is read, so the step under it is
  // inert and hidden from assistive technology, and the black covers it. It stays in the same place
  // in the tree, so the page does not redraw when the answer comes (ADR-018 decision 8).
  const waiting = isWaiting(state) ? state : null;
  const shown = isWaiting(state) ? state.from : state;
  return (
    <>
      <div className="under" inert={waiting !== null} aria-hidden={waiting !== null ? true : undefined}>
        {shown && frame(shown)}
      </div>
      {waiting && <Wait kind={waiting.kind} fade={waiting.from !== undefined} />}
    </>
  );
}

// The scene the GM typed, above every step after the first. Not a chat log: the GM wants the
// character, not the history. On the confirmation it is large and open on the panel, and on the
// character page it is small and dim beside the question, as the sketch draws them (ADR-018).
function ScenePanel({ scene, look = 'well' }: { scene: string; look?: 'well' | 'large' | 'small' }) {
  return (
    <section className={`scene-panel scene-panel-${look}`} aria-label="Your scene">
      <h2 className="scene-panel-title">YOUR SCENE</h2>
      <p>{scene}</p>
    </section>
  );
}
