import clsx from "clsx";
import React from "react";

type SettingProps = Readonly<{
  name: string;
  required?: boolean;
  default?: string;
  note?: string;
  status?: "deprecated" | "removed";
  since?: string;
  replacedBy?: string;
  children: React.ReactNode;
}>;

/** Insert `<wbr>` after each `_` so long keys wrap between tokens. */
function breakableName(name: string): React.ReactNode {
  const chunks: { key: string; text: string; breakAfter: boolean }[] = [];
  let start = 0;
  for (let i = 0; i < name.length; i++) {
    if (name[i] === "_") {
      chunks.push({
        key: name.slice(0, i + 1),
        text: name.slice(start, i + 1),
        breakAfter: true,
      });
      start = i + 1;
    }
  }
  if (start < name.length) {
    chunks.push({ key: name, text: name.slice(start), breakAfter: false });
  }

  return chunks.map((chunk) => (
    <React.Fragment key={chunk.key}>
      {chunk.text}
      {chunk.breakAfter ? <wbr /> : null}
    </React.Fragment>
  ));
}

function toAnchor(name: string): string {
  return name.toLowerCase().match(/[a-z0-9]+/g)?.join("-") ?? "";
}

export default function Setting({
  name,
  required = false,
  default: defaultValue,
  note,
  status,
  since,
  replacedBy,
  children,
}: SettingProps): React.ReactNode {
  const anchor = toAnchor(name);
  const hasTags =
    required || defaultValue !== undefined || Boolean(note) || Boolean(status);
  const statusLabel = status === "removed" ? "Removed" : "Deprecated";

  return (
    <div
      className={clsx("edx-setting", status && `edx-setting--${status}`)}
      id={anchor}
    >
      <div className="edx-setting__key">
        <a className="edx-setting__name" href={`#${anchor}`}>
          <code>{breakableName(name)}</code>
        </a>
        {hasTags && (
          <span className="edx-setting__tags">
            {required && (
              <span className="edx-setting__tag edx-setting__tag--required">
                Required
              </span>
            )}
            {defaultValue !== undefined && (
              <span className="edx-setting__tag">
                Default <code>{defaultValue}</code>
              </span>
            )}
            {status && (
              <span
                className={clsx(
                  "edx-setting__tag",
                  `edx-setting__tag--${status}`,
                )}
              >
                {since ? `${statusLabel} in ${since}` : statusLabel}
              </span>
            )}
            {note && <span className="edx-setting__tag">{note}</span>}
          </span>
        )}
        {replacedBy && (
          <span className="edx-setting__replaced">
            Use{" "}
            <a href={`#${toAnchor(replacedBy)}`}>
              <code>{replacedBy}</code>
            </a>
          </span>
        )}
      </div>
      <div className="edx-setting__body">{children}</div>
    </div>
  );
}
