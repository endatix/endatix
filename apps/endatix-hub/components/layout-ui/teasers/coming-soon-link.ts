import { toast } from "sonner";

/**
 * The default message to be displayed when a feature is not yet available.
 */
export const comingSoonMessage: string = "We are working hard to provide great features to you. Stay tuned. Great stuff is on the way!";

/**
 * Displays a toast message indicating that a feature is coming soon.
 * 
 * @param event The event that triggered the call to this function.
 * @param messageOverride An optional message to override the default coming soon message.
 */
export const showComingSoonMessage = (event: React.MouseEvent<HTMLElement, MouseEvent>, messageOverride?: string) => {
    const messageToShow = messageOverride ?? comingSoonMessage;
    try {
        toast(messageToShow);
    } catch {
        console.warn(`Showing coming soon toast threw an error. Event was raised from ${event.target}`);
    }
};