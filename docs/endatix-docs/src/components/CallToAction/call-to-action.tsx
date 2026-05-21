import Link from "@docusaurus/Link";
import clsx from "clsx";
import { ArrowRight, CircleHelp, Edit3, Headphones, Lightbulb, MessageCircle } from "lucide-react";
import React from "react";

import styles from "./styles.module.css";

type CallToActionLayout = "standalone" | "inline";
type Icon =
  | "lightbulb"
  | "headphones"
  | "question-mark"
  | "arrow-right"
  | "message-circle";
type CtaType =
  | "request-feature"
  | "contact-support"
  | "request-demo"
  | "discuss-use-case";

type CallToActionProps = {
  layout?: CallToActionLayout;
  type?: CtaType;
  className?: string;
  ctaPrompt?: string;
  ctaAction?: string;
  title?: string;
  description?: string;
  actionUrl?: string;
  icon?: Icon;
};

const contactUrl = "https://endatix.com/contact";

type CtaPreset = Required<
  Pick<
    CallToActionProps,
    "ctaAction" | "ctaPrompt" | "description" | "icon" | "title"
  >
> &
  Pick<CallToActionProps, "actionUrl">;

const ctaPresets = {
  "request-feature": {
    title: "Can't find what you need?",
    description:
      "We're constantly expanding Endatix capabilities. Request a specific feature, integration, or schedule a consultation with our technical team to discuss your use case.",
    ctaPrompt: "Need a feature, integration, or provider setup?",
    ctaAction: "Request a feature",
    actionUrl: contactUrl,
    icon: "lightbulb",
  },
  "contact-support": {
    title: "Need implementation guidance?",
    description:
      "Talk with the Endatix team about storage setup, deployment trade-offs, or the right integration path for your environment.",
    ctaPrompt: "Need setup help, a deployment review, or technical consultation?",
    ctaAction: "Contact support",
    actionUrl: contactUrl,
    icon: "headphones",
  },
  "request-demo": {
    title: "Want to see Endatix in action?",
    description:
      "Request a product walkthrough and discuss how Endatix can support your form management and survey workflows.",
    ctaPrompt: "Want a walkthrough or consultation for your use case?",
    ctaAction: "Request a demo",
    actionUrl: contactUrl,
    icon: "question-mark",
  },
  "discuss-use-case": {
    title: "Have a specific use case?",
    description:
      "Tell us what you are trying to build. We can help you evaluate fit, discuss practical options, and decide whether working together makes sense.",
    ctaPrompt: "Have a specific use case or implementation question?",
    ctaAction: "Discuss your use case",
    actionUrl: contactUrl,
    icon: "message-circle",
  },
} satisfies Record<CtaType, CtaPreset>;

const iconMap = {
  lightbulb: Lightbulb,
  headphones: Headphones,
  "question-mark": CircleHelp,
  "arrow-right": ArrowRight,
  "message-circle": MessageCircle,
} satisfies Record<Icon, typeof Lightbulb>;

/**
 * CallToAction component
 * @param layout - The layout of the call to action
 * @param type - The preset content to use
 * @param className - The class name of the call to action
 * @param ctaPrompt - The prompt of the call to action
 * @param ctaAction - The action of the call to action
 * @param icon - The icon of the call to action
 * @param actionUrl - The url of the call to action
 */
export default function CallToAction({
  layout = "standalone",
  type = "request-feature",
  className,
  ctaPrompt,
  ctaAction,
  title,
  description,
  actionUrl,
  icon,
}: CallToActionProps): React.ReactNode {
  const preset = ctaPresets[type];
  const copy = {
    title: title ?? preset.title,
    description: description ?? preset.description,
    ctaPrompt: ctaPrompt ?? preset.ctaPrompt,
    ctaAction: ctaAction ?? preset.ctaAction,
    actionUrl: actionUrl ?? preset.actionUrl ?? contactUrl,
    icon: icon ?? preset.icon,
  };
  const PromptIcon = iconMap[copy.icon];

  if (layout === "inline") {
    return (
      <aside className={clsx(styles.inline, className)}>
        <div className={styles.inlinePrompt}>
          <PromptIcon aria-hidden="true" size={18} strokeWidth={2.2} />
          <span>{copy.ctaPrompt}</span>
        </div>

        <Link className={styles.inlineButton} to={copy.actionUrl}>
          {copy.ctaAction}
          <ArrowRight aria-hidden="true" size={16} strokeWidth={2.2} />
        </Link>
      </aside>
    );
  }

  return (
    <aside className={clsx(styles.standalone, className)}>
      <div className={styles.standaloneContent}>
        <div className={styles.iconBox}>
          <PromptIcon aria-hidden="true" size={28} strokeWidth={2.1} />
        </div>

        <div>
          <h3 className={styles.title}>{copy.title}</h3>
          <p className={styles.description}>{copy.description}</p>
        </div>
      </div>

      <Link className={styles.standaloneButton} to={copy.actionUrl}>
        <Edit3 aria-hidden="true" size={17} strokeWidth={2.2} />
        {copy.ctaAction}
      </Link>
    </aside>
  );
}
