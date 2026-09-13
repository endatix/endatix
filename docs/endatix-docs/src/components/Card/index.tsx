import React from "react";
import Icon, { type IconAccent, type IconName } from "@site/src/components/Icon";

type CardProps = Readonly<{
  eyebrow?: string;
  title: string;
  icon?: IconName;
  accent?: IconAccent;
  /** Short prose under the title. Prefer this over wrapping a paragraph in children. */
  lede?: React.ReactNode;
  footer?: React.ReactNode;
  children?: React.ReactNode;
}>;

/**
 * Generic fact / overview card. Children are free-form — often `<Specs>`.
 */
export default function Card({
  eyebrow,
  title,
  icon,
  accent,
  lede,
  footer,
  children,
}: CardProps): React.ReactNode {
  return (
    <div className="card edx-card">
      <div className="edx-card__head">
        {icon ? <Icon name={icon} accent={accent} /> : null}
        <div>
          {eyebrow ? <p className="edx-card__eyebrow">{eyebrow}</p> : null}
          <h3 className="edx-card__title">{title}</h3>
        </div>
      </div>
      {lede ? <p className="edx-card__lede">{lede}</p> : null}
      {children}
      {footer ? <div className="edx-card__footer">{footer}</div> : null}
    </div>
  );
}
