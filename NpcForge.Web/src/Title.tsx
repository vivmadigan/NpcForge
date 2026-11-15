// The forge page's big title, drawn as a picture of pixel letters, not as text, so the one thing over
// the cavern needs no flat colour under it (epic 13 constraint; ADR-017, Consequences). Its text
// alternative is NPC FORGE. Each letter is a 5 by 7 map; every cell's colour is a class, whose colour
// is a token (ADR-017 decision 5).

const letters: Record<string, string[]> = {
  N: ['X...X', 'XX..X', 'X.X.X', 'X..XX', 'X...X', 'X...X', 'X...X'],
  P: ['XXXX.', 'X...X', 'X...X', 'XXXX.', 'X....', 'X....', 'X....'],
  C: ['.XXX.', 'X...X', 'X....', 'X....', 'X....', 'X...X', '.XXX.'],
  F: ['XXXXX', 'X....', 'X....', 'XXXX.', 'X....', 'X....', 'X....'],
  O: ['.XXX.', 'X...X', 'X...X', 'X...X', 'X...X', 'X...X', '.XXX.'],
  R: ['XXXX.', 'X...X', 'X...X', 'XXXX.', 'X.X..', 'X..X.', 'X...X'],
  G: ['.XXX.', 'X...X', 'X....', 'X.XXX', 'X...X', 'X...X', '.XXX.'],
  E: ['XXXXX', 'X....', 'X....', 'XXXX.', 'X....', 'X....', 'XXXXX'],
};
const HEIGHT = 7;

// The molten bands, top to bottom, one class a row: two rows each of the three flame colours, then
// one of the deepest. The sketch's gradient runs the same four colours in the same order.
const bands = ['title-core', 'title-core', 'title-hot', 'title-hot', 'title-flame', 'title-flame', 'title-deep'];

type Cell = { x: number; y: number };

// Each word on its own line, centred, with a cell between letters and three between words on a line.
function layout(lines: string[][]): { cells: Cell[]; width: number; height: number } {
  const widthOf = (words: string[]) =>
    words.reduce((total, word, w) => total + (w > 0 ? 3 : 0) + word.length * 6 - 1, 0);
  const width = Math.max(...lines.map(widthOf));
  const cells: Cell[] = [];
  lines.forEach((words, line) => {
    let x = Math.floor((width - widthOf(words)) / 2);
    for (const [w, word] of words.entries()) {
      if (w > 0) x += 3;
      for (const letter of word) {
        letters[letter].forEach((row, y) =>
          [...row].forEach((on, dx) => {
            if (on === 'X') cells.push({ x: x + dx, y: line * (HEIGHT + 2) + y });
          }),
        );
        x += 6;
      }
      x -= 1;
    }
  });
  return { cells, width, height: lines.length * (HEIGHT + 2) - 2 };
}

// One picture. Three layers, so each letter has a notched outline and a lip of depth under it.
function Picture({ lines, className }: { lines: string[][]; className: string }) {
  const { cells, width, height } = layout(lines);
  const OUT = 0.5; // the outline's width, in cells
  const LIP = 0.75; // how far the lip hangs below the outline
  return (
    <svg
      className={className}
      viewBox={`-1 -1 ${width + 2} ${height + 2 + LIP}`}
      role="img"
      aria-label="NPC FORGE"
      shapeRendering="crispEdges"
    >
      <g className="title-lip">
        {cells.map(({ x, y }) => (
          <rect key={`${x},${y}`} x={x - OUT} y={y - OUT + LIP} width={1 + 2 * OUT} height={1 + 2 * OUT} />
        ))}
      </g>
      <g className="title-outline">
        {cells.map(({ x, y }) => (
          <rect key={`${x},${y}`} x={x - OUT} y={y - OUT} width={1 + 2 * OUT} height={1 + 2 * OUT} />
        ))}
      </g>
      <g>
        {cells.map(({ x, y }) => (
          <rect key={`${x},${y}`} x={x} y={y} width={1} height={1} className={bands[y % (HEIGHT + 2)]} />
        ))}
      </g>
    </svg>
  );
}

// Two lines on a phone and one at desktop width, as the sketch does; the stylesheet shows one of the
// two pictures, and the one hidden is out of the accessibility tree.
export function Title() {
  return (
    <h1 className="title">
      <Picture lines={[['NPC'], ['FORGE']]} className="title-stacked" />
      <Picture lines={[['NPC', 'FORGE']]} className="title-wide" />
    </h1>
  );
}
