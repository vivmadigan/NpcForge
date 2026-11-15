import type { ConfirmField } from './api';

// Every field in the order sent, never regrouped (ADR-017 decision 3). A field with the GM's words
// is drawn large with where they came from, and a field with no value is one quiet line, so the eye
// lands on what the GM said and the rolled fields do not read as a wall.
export function Confirmation({ fields }: { fields: ConfirmField[] }) {
  return (
    <ol className="fields">
      {fields.map((field) =>
        field.label === 'my words' || field.label === 'inferred' ? (
          <li key={field.field} className="field field-told">
            <span className="field-name">{field.field}</span>
            <Label label={field.label} />
            <span className="field-value">{field.value}</span>
            {field.from === 'typed when asked' ? (
              <span className="field-from">typed when asked</span>
            ) : (
              <>
                <span className="field-from">from “{field.from.quote}”</span>
                <span className="field-from">in “{field.from.sentence}”</span>
              </>
            )}
          </li>
        ) : (
          <li key={field.field} className="field field-quiet">
            <span className="field-name">{field.field}</span>
            <Label label={field.label} />
            <span className="field-note">{field.note}</span>
          </li>
        ),
      )}
    </ol>
  );
}

// Each label has its own shape as well as its own words, so the four tell apart at a glance and not
// by colour alone.
function Label({ label }: { label: ConfirmField['label'] }) {
  return <span className={`label label-${label.replace(' ', '-')}`}>{label}</span>;
}
