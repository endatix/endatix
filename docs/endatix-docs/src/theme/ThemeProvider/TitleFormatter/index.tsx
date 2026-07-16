import { TitleFormatterProvider } from "@docusaurus/theme-common/internal";
import type { ReactNode } from "react";

// Derived from the provider's own prop type instead of importing
// TitleFormatterFnWithDefault directly — that type isn't consistently
// re-exported from theme-common/internal across patch versions.
type TitleFormatterFnWithDefault = Parameters<
  typeof TitleFormatterProvider
>[0]["formatter"];

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
