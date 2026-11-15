// The forge page's first step: the scene in one box, one main button, and a real example. After
// the brief is turned down or a character left, it comes back with the scene still in the box and a
// line that says what was not done.

// Page copy, not made-up data: the bakery run's scene, so a friend sees what a good one looks like.
const exampleScene =
  'The party bursts into a tiny bakery where a goblin baker is furious that someone keeps stealing ' +
  'his croissants. It’s a silly session, play it for laughs.';

// The console's opening prompt (NpcForge.Core, SceneInterview.Greeting), without its line about
// pressing Enter.
const greeting =
  'Describe the scene in one go: where the players are, who they meet, what they want from that ' +
  'person, and anything you already know about them.';

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
      <h1 className="page-title">DESCRIBE THE SCENE</h1>
      {after === 'turned-down' && (
        <p className="notice">
          Not rolled. You turned the brief down. Your scene is still in the box, so you can change it
          and send it again.
        </p>
      )}
      {after === 'left' && (
        <p className="notice">
          Not saved. You left the character. Your scene is still in the box, so you can change it and
          send it again.
        </p>
      )}
      <label className="instruction" htmlFor="scene">
        {greeting}
      </label>
      <textarea
        id="scene"
        className="scene-box"
        rows={6}
        value={scene}
        onChange={(event) => onScene(event.target.value)}
      />
      <button className="button button-main" type="submit" disabled={!ready}>
        SEND THE SCENE
      </button>
      <div className="example">
        <h2 className="example-title">AN EXAMPLE</h2>
        <p>{exampleScene}</p>
        <button className="button" type="button" onClick={() => onScene(exampleScene)}>
          USE THIS EXAMPLE
        </button>
      </div>
    </form>
  );
}
