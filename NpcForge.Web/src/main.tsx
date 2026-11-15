import { createRoot } from 'react-dom/client';
import '@fontsource/press-start-2p';
import '@fontsource/im-fell-english';
import '@fontsource/im-fell-english/400-italic.css';
import '@fontsource/crimson-text';
import './tokens.css';
import './app.css';
import { Frame, useSavedCount } from './Frame';
import { SavedPage } from './SavedPage';

// Two pages and one number in the path, so no router (ADR-017, "Smaller choices").
function Page() {
  const count = useSavedCount();
  const path = location.pathname;
  const opened = path.match(/^\/saved\/(\d+)$/);
  if (path === '/saved') return <SavedPage number={null} />;
  if (opened) return <SavedPage number={Number(opened[1])} />;
  if (path === '/') {
    return (
      <Frame saved={count}>
        <p>The forge page comes next.</p>
        <a className="back" href="/saved">
          OPEN THE SAVED CHARACTERS
        </a>
      </Frame>
    );
  }
  return (
    <Frame saved={count}>
      <p>No page at {path}</p>
    </Frame>
  );
}

createRoot(document.getElementById('root')!).render(<Page />);
