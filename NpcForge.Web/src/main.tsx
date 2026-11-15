import { createRoot } from 'react-dom/client';
import '@fontsource/press-start-2p';
import '@fontsource/im-fell-english';
import '@fontsource/im-fell-english/400-italic.css';
import '@fontsource/crimson-text';
import './tokens.css';
import './app.css';
import { ForgePage } from './ForgePage';
import { Frame, useSavedCount } from './Frame';
import { SavedPage } from './SavedPage';
import { namedStates } from './madeUp/states';

// Two pages and one number in the path, so no router (ADR-017, "Smaller choices").
function Page() {
  // Read once here and handed down, so one place owns the header's count.
  const { count, recount } = useSavedCount();
  const path = location.pathname;

  // ?state=<name> opens a page on a named value from the table (ADR-017 decision 6). A name the
  // table lacks, or one opened on another path than its own, is never the default page.
  const name = new URLSearchParams(location.search).get('state');
  const named = name !== null && Object.hasOwn(namedStates, name) ? namedStates[name] : undefined;
  if (name !== null && named?.path !== path) {
    return (
      <Frame saved={count}>
        <div className="panel">
          <p>No state named {name}</p>
        </div>
      </Frame>
    );
  }

  const opened = path.match(/^\/saved\/(\d+)$/);
  const savedStart = named?.page === 'saved' ? named.state : undefined;
  if (path === '/saved') return <SavedPage number={null} saved={count} start={savedStart} />;
  if (opened) return <SavedPage number={Number(opened[1])} saved={count} start={savedStart} />;
  if (path === '/') {
    return <ForgePage saved={count} recount={recount} start={named?.page === 'forge' ? named.state : undefined} />;
  }
  return (
    <Frame saved={count}>
      <div className="panel">
        <p>No page at {path}</p>
      </div>
    </Frame>
  );
}

createRoot(document.getElementById('root')!).render(<Page />);
