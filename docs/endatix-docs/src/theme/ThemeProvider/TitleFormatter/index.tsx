import { TitleFormatterProvider } from "@docusaurus/theme-common/internal";
import type { TitleFormatterFnWithDefault } from "@docusaurus/theme-common/internal";
import type { ReactNode } from "react";

// Pages rendered by the content-pages plugin (i.e. src/pages, including the
// homepage) keep their raw <title>, skipping the "| Site Title" suffix that
// the default formatter appends everywhere else.
const formatter: TitleFormatterFnWithDefault = (params) => {
  if (params.plugin.name === "docusaurus-plugin-content-pages") {
    return params.title.trim() || params.siteTitle;
  }
  return params.defaultFormatter(params);
};

export default function ThemeProviderTitleFormatter({
  children,
}: {
  children: ReactNode;
}): ReactNode {
  return (
    <TitleFormatterProvider formatter={formatter}>
      {children}
    </TitleFormatterProvider>
  );
}
