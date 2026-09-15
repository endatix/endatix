import clsx from "clsx";
import React from "react";

type SettingProps = Readonly<{
  /** The key as it is written in config — `ENDATIX_BASE_URL`, `Endatix:Jwt:SigningKey`. */
  name: string;
  /** Marks the key as required to boot. */
  required?: boolean;
  /** Effective value when the key is unset. Omit when there is no meaningful default. */
  default?: string;
  /** Short qualifier shown beside the name — "server only", "build time", "one of these two". */
  note?: string;
  /** Lifecycle state. `deprecated` still works; `removed` no longer does. */
  status?: "deprecated" | "removed";
  /** Release the status applies to, e.g. `0.7.6`. Shown as "Deprecated in 0.7.6". */
  since?: string;
  /** Current key that supersedes this one. Rendered as a link to its entry on the page. */
  replacedBy?: string;
  /** What the key does, then what to set it to. One or two sentences. */
  children: React.ReactNode;
}>;

/**
 * Renders the key with a break opportunity after each underscore, so a long name wraps as
 * `NEXT_PUBLIC_SUBMITTER_` / `PRIMARY_FILTER_LABEL` instead of splitting mid-word at the
 * column edge. `<wbr>` adds no character, so the name still copies as one string.
 */
function breakableName(name: string): React.ReactNode {
  const parts = name.split(/(?<=_)/);
  return parts.map((part, index) => (
    <React.Fragment key={index}>
      {part}
      {index < parts.length - 1 && <wbr />}
    </React.Fragment>
  ));
}

/** Slug for the anchor: lowercase, non-alphanumerics collapsed, no leading or trailing dash. */
function toAnchor(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

/**
 * One configuration key: name, how it behaves when unset, and what it does.
 *
 * The name carries an `id`, so a reader or an agent can link to a single key
 * (`…/environment#endatix-base-url`) rather than to the section that contains it.
 */
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
