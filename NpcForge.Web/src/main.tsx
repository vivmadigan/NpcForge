import { createRoot } from 'react-dom/client';
import '@fontsource/press-start-2p';
import '@fontsource/im-fell-english';
import '@fontsource/im-fell-english/400-italic.css';
import '@fontsource/crimson-text';
import './tokens.css';
import './app.css';
import { Frame, useSavedCount } from './Frame';
import { SavedPage } from './SavedPage';
import { namedStates } from './madeUp/states';

// Two pages and one number in the path, so no router (ADR-017, "Smaller choices").
function Page() {
  // Read once here and handed down, so one place owns the bar's count.
  const count = useSavedCount();
  const path = location.pathname;

  // ?state=<name> opens a page on a named value from the table (ADR-017 decision 6). A name the
  // table lacks, or one opened on another path than its own, is never the default page.
  const name = new URLSearchParams(location.search).get('state');
  const named = name !== null && Object.hasOwn(namedStates, name) ? namedStates[name] : undefined;
  if (name !== null && named?.path !== path) {
    return (
      <Frame saved={count}>
        <p>No state named {name}</p>
      </Frame>
    );
  }

  const opened = path.match(/^\/saved\/(\d+)$/);
  if (path === '/saved') return <SavedPage number={null} saved={count} start={named?.state} />;
  if (opened) return <SavedPage number={Number(opened[1])} saved={count} start={named?.state} />;
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
