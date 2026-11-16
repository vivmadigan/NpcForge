import { useEffect, useRef, useState, type ReactElement } from 'react';
import {
  answer,
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
import { forge, Sprite } from './Sprite';
import { Title } from './Title';

type Written = Extract<RunResult, { kind: 'written' }>;

// The page holds one of these and draws from it with one switch (principle-model-the-domain). Each
// is plain data, so a state can be opened by name (ADR-017 decision 6). Every step after the first
// carries the scene, which stays on screen above it. Changing the scene after the confirmation and
// leaving are not seam calls (ADR-017 decision 3); they put the scene back in the box.
export type ForgeState =
  | { kind: 'describe'; scene: string }
  | { kind: 'turned-down'; scene: string } // nothing was rolled
  | { kind: 'left'; scene: string } // nothing was saved
  | { kind: 'reading'; scene: string } // waiting on the interviewer
  | { kind: 'question'; scene: string; interview: InterviewId; field: string; question: string; answer: string }
  | { kind: 'confirm'; scene: string; interview: InterviewId; entries: ConfirmEntry[] }
  | { kind: 'writing'; scene: string; interview: InterviewId }
  | { kind: 'written'; scene: string; interview: InterviewId; character: Written }
  | { kind: 'no-name'; scene: string; interview: InterviewId; reason: string }
  | { kind: 'stopped'; scene: string; reason: string } // the spending cap
  | { kind: 'saved'; scene: string; interview: InterviewId; number: number };

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
  // that has gone. Typing keeps the step, so only a change of step does this.
  const step = useRef<HTMLDivElement>(null);
  useEffect(() => {
    window.scrollTo(0, 0);
    step.current?.focus({ preventScroll: true });
  }, [state.kind]);

  async function send(scene: string) {
    setState({ kind: 'reading', scene });
    setState(afterInterview(scene, await startInterview(scene)));
  }
  async function reply(scene: string, interview: InterviewId, text: string) {
    setState({ kind: 'reading', scene });
    setState(afterInterview(scene, await answer(interview, text)));
  }
  async function roll(scene: string, interview: InterviewId) {
    setState({ kind: 'writing', scene, interview });
    setState(afterRoll(scene, interview, await rollAndWrite(interview)));
  }
  async function keep(scene: string, interview: InterviewId, character: Written) {
    const number = await save(character.draft);
    recount();
    setState({ kind: 'saved', scene, interview, number });
  }

  // The return type has no `undefined` in it, so a state with no case here fails tsc and cannot
  // ship as a blank page (ADR-017, "Outcomes as values").
  function draw(): ReactElement {
    switch (state.kind) {
      case 'describe':
      case 'turned-down':
      case 'left': {
        return (
          <SceneForm
            scene={state.scene}
            after={state.kind === 'describe' ? null : state.kind}
            onScene={(scene) => setState({ ...state, scene })}
            onSend={send}
          />
        );
      }
      case 'reading':
        return (
          <>
            <ScenePanel scene={state.scene} />
            <h1 className="page-title">READING THE SCENE</h1>
            <p>The interviewer is reading your scene for what it already says.</p>
          </>
        );
      case 'question': {
        const { scene, interview, answer: text } = state;
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
              <span className="field-name">{state.field}</span> {state.question}
            </label>
            <input
              id="answer"
              className="answer-box"
              value={text}
              onChange={(event) => setState({ ...state, answer: event.target.value })}
            />
            <button className="button button-main" type="submit" disabled={text.trim() === ''}>
              ANSWER
            </button>
          </form>
        );
      }
      case 'confirm': {
        const { scene, interview } = state;
        return (
          <>
            <ScenePanel scene={scene} look="large" />
            <Confirmation entries={state.entries} />
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
      case 'writing':
        return (
          <>
            <ScenePanel scene={state.scene} />
            <div className="forge">
              <Sprite art={forge} scale={6} className="forge-art" />
              <h1 className="page-title">AT THE FORGE</h1>
              {/* The console's order: the brief, then the name, then the writer. Still text, as
                  nothing on the page moves (the epic's default). */}
              <ol className="forge-steps">
                <li>The dice settle who they are.</li>
                <li>A name follows the dice.</li>
                <li>The character is written from the brief and your scene.</li>
              </ol>
              <p>This takes a little while. Nothing is saved until you say so.</p>
            </div>
          </>
        );
      case 'written': {
        const { scene, interview, character } = state;
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
        const { scene, interview } = state;
        return (
          <>
            <ScenePanel scene={scene} />
            <h1 className="page-title">NO NAME</h1>
            <p className="notice">Not written: {state.reason}</p>
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
            <ScenePanel scene={state.scene} />
            <h1 className="page-title">STOPPED</h1>
            <p className="notice">Stopped: {state.reason} Nothing was saved.</p>
            <p>Your saved characters are still there.</p>
            <div className="actions">
              <a className="button" href="/saved">
                OPEN THE SAVED CHARACTERS
              </a>
            </div>
          </>
        );
      case 'saved': {
        const { scene, interview, number } = state;
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
  const first = state.kind === 'describe' || state.kind === 'turned-down' || state.kind === 'left';
  const written = state.kind === 'written';
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
        {written ? draw() : <div className="panel">{draw()}</div>}
      </div>
    </Frame>
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
