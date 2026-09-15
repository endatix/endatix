import Card from "@site/src/components/Card";
import CardGrid from "@site/src/components/CardGrid";
import Icon from "@site/src/components/Icon";
import LinkCard from "@site/src/components/LinkCard";
import Pill from "@site/src/components/Pill";
import Setting from "@site/src/components/Setting";
import Settings from "@site/src/components/Settings";
import Spec from "@site/src/components/Spec";
import Specs from "@site/src/components/Specs";
import MDXComponents from "@theme-original/MDXComponents";

/**
 * Extra MDX tags (PascalCase). See https://docusaurus.io/docs/markdown-features/react#mdx-component-scope
 */
export default {
  ...MDXComponents,
  Card,
  CardGrid,
  Icon,
  LinkCard,
  Pill,
  Setting,
  Settings,
  Spec,
  Specs,
};
