import React, {type ReactNode} from "react";
import Icon from "@theme-original/DocCard/Heading/Icon";
import type {Props} from "@theme/DocCard/Heading/Icon";
import type {PropSidebarItem} from "@docusaurus/plugin-content-docs";
import type {LucideIcon} from "lucide-react";
import {
  AlignLeft,
  AppWindow,
  ArrowRightFromLine,
  Calculator,
  CircleCheck,
  EyeOff,
  FileDown,
  FileSearch,
  FileUp,
  GitBranch,
  GlobeLock,
  Grid3x3,
  Hourglass,
  Image,
  KeyRound,
  Languages,
  LayoutList,
  LayoutTemplate,
  ListChecks,
  Mail,
  MonitorCog,
  PanelsTopLeft,
  PencilRuler,
  Repeat,
  Route,
  Settings2,
  Share2,
  ShieldCheck,
  Shuffle,
  Sigma,
  SlidersHorizontal,
  SquareFunction,
  Star,
  Table2,
  TextCursorInput,
  TextQuote,
  Type,
  UserCheck,
  UserCog,
  Zap,
} from "lucide-react";

/**
 * End-user generated-index cards. Keyed by the doc path tail so a new
 * sibling page keeps the default document mark until it is added here.
 */
const END_USER_CARD_ICONS: Record<string, LucideIcon> = {
  "forms/form-builder": PencilRuler,
  "forms/translation-and-localization": Languages,
  "forms/form-settings": Settings2,
  "forms/prefilling-and-personalization": TextCursorInput,
  "sharing/public-and-private-forms": GlobeLock,
  "sharing/embedding": AppWindow,
  "sharing/single-submission": UserCheck,
  "submissions/viewing-submissions": Table2,
  "submissions/submission-details": FileSearch,
  "submissions/sharing-submissions": Share2,
  "submissions/exporting-submissions": FileDown,
  "administration/platform-admins": UserCog,
  "administration/auth-settings": KeyRound,
  "administration/email-settings": Mail,
  "administration/environment": MonitorCog,
  "form-builder/form-display-modes": LayoutTemplate,
  "form-builder/form-navigation": Route,
  "form-builder/survey-logo": Image,
  "form-builder/question-settings": SlidersHorizontal,
  "form-builder/thank-you-page": CircleCheck,
  "form-builder/conditional-logic": GitBranch,
  "form-builder/logic-expressions": SquareFunction,
  "form-builder/custom-variables": Calculator,
  "form-builder/triggers": Zap,
  "form-builder/validation": ShieldCheck,
  "form-builder/panels": PanelsTopLeft,
  "form-builder/question-loops": Repeat,
  "form-builder/randomization-of-choices": Shuffle,
  "form-builder/blind-search": EyeOff,
  "form-builder/carry-forward": ArrowRightFromLine,
  "form-builder/text-piping": TextQuote,
  "form-builder/rich-text-formatting": Type,
  "form-builder/question-types": LayoutList,
  "question-types/text-questions": AlignLeft,
  "question-types/choice-questions": ListChecks,
  "question-types/rating-and-ranking-questions": Star,
  "question-types/matrix-questions": Grid3x3,
  "question-types/file-upload-question": FileUp,
  "question-types/display-and-calculated-widgets": Sigma,
  "forms/submission-expiration": Hourglass,
};

function hrefOf(item: PropSidebarItem): string | undefined {
  if (item.type === "link" || item.type === "category") {
    return item.href;
  }
  return undefined;
}

function iconFor(item: PropSidebarItem): LucideIcon | undefined {
  const href = hrefOf(item);
  if (!href) {
    return undefined;
  }
  const path = href.split("?")[0].replace(/\/$/, "");
  const match = Object.keys(END_USER_CARD_ICONS).find((key) =>
    path.endsWith(`/${key}`),
  );
  return match ? END_USER_CARD_ICONS[match] : undefined;
}

export default function DocCardHeadingIcon({
  icon,
  item,
}: Props): ReactNode {
  const Glyph = iconFor(item);

  return (
    <Icon
      item={item}
      icon={
        Glyph ? (
          <Glyph size={18} strokeWidth={2} aria-hidden="true" />
        ) : (
          icon
        )
      }
    />
  );
}
