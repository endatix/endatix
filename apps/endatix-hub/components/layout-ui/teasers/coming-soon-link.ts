import { toast } from "@/components/ui/toast";

/**
 * The default title to be displayed when a feature is not yet available.
 */
export const comingSoonTitle: string =
  "We are working hard to provide great features to you"

  /**
 * The default description to be displayed when a feature is not yet available.
 */
export const comingSoonDescription: string =
  "Stay tuned. Great stuff is on the way!";

/**
 * Displays a toast message indicating that a feature is coming soon.
 *
 * @param titleOverride An optional title to override the default coming soon title.
 * @param descriptionOverride An optional description to override the default coming soon description.
 */
export const showComingSoonMessage = (
  titleOverride?: string,
  descriptionOverride?: string,
) => {
  const title = titleOverride ?? comingSoonTitle;
  const description = descriptionOverride ?? comingSoonDescription;
  toast.info({
    title,
    description,
  });
};
