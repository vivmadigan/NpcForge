// The forge's pixel art, drawn in code: each sprite is a map of characters, one per pixel, and each
// character names a colour class whose colour is a token (ADR-017 decision 5). No image files.

// What each character in a map paints: a class whose colour is the token named `--` plus the class
// (the cavern draws the forge on a canvas, where it reads that token). A dot is empty.
export const paint: Record<string, string> = {
  k: 'px-mortar',
  s: 'px-stone',
  S: 'px-stone-lit',
  r: 'px-glow',
  o: 'px-flame',
  y: 'px-flame-hot',
  w: 'px-flame-core',
  i: 'px-iron-dark',
  I: 'px-iron',
  L: 'px-iron-lit',
  d: 'px-die',
  p: 'px-pip',
};

// A stone forge with a fire in its mouth and an anvil on the hearth: the wait screen.
export const forge = [
  '.............SSSSSSSk.......................',
  '.............sssssssk.......................',
  '.............sssssssk.......................',
  '.............kkkkkkkk.......................',
  '.............SSSkSSSS.......................',
  '.............ssskssss.......................',
  '.............ssskssss.......................',
  '.SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS...........',
  '.kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk...........',
  '...ssssssskssssssskssssssskssss.............',
  '...ssssssskssssssskssssssskssss.............',
  '...kkkkkkkkkkkkkkkkkkkkkkkkkkkk.............',
  '...SSSkSSSSSSSkSSSSSSSkSSSSSSSk.............',
  '...ssskssssssskkkkksssksssssssk.............',
  '...sssksssssskkrrrrkksksssssssk.............',
  '...kkkkkkkkkkrrrrrrrrkkkkkkkkkk.............',
  '...SSSSSSSkkrrrrrorrrrkSSSkSSSS.............',
  '...ssssssskrrorrrorrrorksskkkkkkkkkkkkk.....',
  '...ssssssskroorroooorroksskLLLLLLLLLLLLkkkk.',
  '...kkkkkkkrooorooyooroookkIIIIIIIIIIIIIILLLk',
  '...SSSkSSkroyoooyyyoooyokSkkIIIIIIIIIIIIkkk.',
  '...sssksskrooyooyyyyoyyokssskkiIIIIIikk.....',
  '...sssksskroyyyoyywyyyyokssssskIIIIik.......',
  '...kkkkkkkroyyyyywwwyyyokkkkkkkIIIIik.......',
  '...SSSSSSkooyyywwwwwwyyokSkSkkiIIIIIIikk....',
  '...sssssskoyywwwwwwwwwyykskkiiiiiiiiiiik....',
  '.SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS.',
  '.kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk.',
];

// A small anvil, by the header's name.
export const anvil = [
  '.kkkkkkkkk...',
  'kLLLLLLLLLkkk',
  'kIIIIIIIIIILk',
  '.kIIIIIIIkk..',
  '...kIIIk.....',
  '...kIIIk.....',
  '..kiIIIik....',
  '.kiiiiiiik...',
];

// A die showing five, on the `rolled` label.
export const die = [
  '.ddddd.',
  'dpdddpd',
  'ddddddd',
  'dddpddd',
  'ddddddd',
  'dpdddpd',
  '.ddddd.',
];

// Each run of one character in a row is one rectangle, with hard edges, so a pixel stays a square
// at any whole-number scale. Hidden from screen readers: the words beside it say the same.
export function Sprite({ art, scale, className }: { art: string[]; scale: number; className?: string }) {
  const rects = [];
  for (const [y, row] of art.entries()) {
    for (let x = 0; x < row.length; ) {
      let end = x + 1;
      while (row[end] === row[x]) end += 1;
      if (row[x] !== '.') {
        rects.push(<rect key={`${x},${y}`} x={x} y={y} width={end - x} height={1} className={paint[row[x]]} />);
      }
      x = end;
    }
  }
  return (
    <svg
      className={className}
      viewBox={`0 0 ${art[0].length} ${art.length}`}
      width={art[0].length * scale}
      height={art.length * scale}
      shapeRendering="crispEdges"
      aria-hidden="true"
    >
      {rects}
    </svg>
  );
}
