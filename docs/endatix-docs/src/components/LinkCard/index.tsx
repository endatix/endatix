import Link from "@docusaurus/Link";
import React from "react";
import Icon, { type IconName } from "@site/src/components/Icon";

type LinkCardProps = Readonly<{
  to: string;
  title: string;
  description: string;
  icon?: IconName;
}>;

export default function LinkCard({
  to,
  title,
  description,
  icon,
}: LinkCardProps): React.ReactNode {
  return (
    <Link className="card edx-linkcard" to={to}>
      {icon ? <Icon name={icon} /> : null}
      <span className="edx-linkcard__title">{title}</span>
      <span className="edx-linkcard__desc">{description}</span>
    </Link>
  );
}
