"use client";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import { ChangePasswordForm } from "./change-password-form";
import { Button, ButtonProps } from "@/components/ui/button";
import { ChevronUp, Pencil } from "lucide-react";
import { useState } from "react";
import { Separator } from "@/components/ui/separator";
import React from "react";

function ChangePassword() {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
      <div className="space-y-2">
        <div className="flex flex-row justify-between">
          <div>
            <h3 className="text-lg font-medium">Change Password</h3>
            <p className="text-sm text-muted-foreground">
              Strengthen your account security by changing your password.
            </p>
          </div>
          <CollapsibleTrigger asChild>
            <ChangePasswordButton isOpen={isOpen} />
          </CollapsibleTrigger>
        </div>
        <Separator />
        <CollapsibleContent className="space-y-2">
          <ChangePasswordForm />
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}

interface ChangePasswordButtonProps extends ButtonProps {
  isOpen: boolean;
}

const ChangePasswordButton = React.forwardRef<
  HTMLButtonElement,
  ChangePasswordButtonProps
>(({ isOpen, ...props }, ref) => {
  const icon = isOpen ? (
    <ChevronUp className="w-4 h-4" />
  ) : (
    <Pencil className="w-4 h-4" />
  );
  const text = isOpen ? "Cancel" : "Change Password";

  return (
    <Button variant="ghost" {...props} ref={ref}>
      {icon}
      {text}
    </Button>
  );
});

ChangePasswordButton.displayName = "ChangePasswordButton";

export { ChangePassword };
