// The forge page's first step, as the cavern sketch draws it: the scene in one box, one main button,
// and a link that puts a real example in the box. After the brief is turned down or a character
// left, it comes back with the scene still in the box and a line that says what was not done.

// Page copy, not made-up data: the bakery run's scene, so a friend sees what a good one looks like.
const exampleScene =
  'The party bursts into a tiny bakery where a goblin baker is furious that someone keeps stealing ' +
  'his croissants. It’s a silly session, play it for laughs.';

// What the console's opening prompt asks for (NpcForge.Core, SceneInterview.Greeting). The box's
// label already says "describe your scene", so this line starts at what to put in it.
const help =
  'Where the players are, who they meet, what they want from that person, and anything you already ' +
  'know about them.';

export function SceneForm({
  scene,
  after,
  onScene,
  onSend,
}: {
  scene: string;
  after: 'turned-down' | 'left' | null;
  onScene: (scene: string) => void;
  onSend: (scene: string) => void;
}) {
  const ready = scene.trim() !== '';
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        if (ready) onSend(scene.trim());
      }}
    >
      {/* The heading is the box's label, so the box is named by the words above it. */}
      <h2 className="page-title">
        <label htmlFor="scene">DESCRIBE YOUR SCENE</label>
      </h2>
      {after === 'turned-down' && (
        <p className="notice">
          Not rolled. Your scene is still in the box, so you can change it and send it again.
        </p>
      )}
      {after === 'left' && (
        <p className="notice">
          Not saved. You left the character. Your scene is still in the box, so you can change it and
          send it again.
        </p>
      )}
      <p className="instruction" id="scene-help">
        {help}
      </p>
      <textarea
        id="scene"
        className="scene-box"
        aria-describedby="scene-help"
        rows={5}
        value={scene}
        onChange={(event) => onScene(event.target.value)}
      />
      <div className="actions">
        <button className="button button-main" type="submit" disabled={!ready}>
          SEND THE SCENE
        </button>
        <button className="plain" type="button" onClick={() => onScene(exampleScene)}>
          or try the bakery example
        </button>
      </div>
    </form>
  );
}
