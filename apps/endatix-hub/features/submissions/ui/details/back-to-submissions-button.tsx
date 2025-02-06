import { Button, ButtonProps } from "@/components/ui/button";
import { ArrowLeftIcon } from "lucide-react";
import Link from "next/link";

interface BackToSubmissionsButtonProps extends ButtonProps {
  formId: string;
  text?: string;
}

export function BackToSubmissionsButton({
  formId,
  text = "Back to submissions",
  variant,
  ...props
}: BackToSubmissionsButtonProps) {
  return (
    <Button variant={variant} asChild {...props}>
      <Link
        href={`/forms/${formId}/submissions`}
        className="flex items-center gap-2"
      >
        <ArrowLeftIcon className="w-4 h-4" />
        {text}
      </Link>
    </Button>
  );
}
