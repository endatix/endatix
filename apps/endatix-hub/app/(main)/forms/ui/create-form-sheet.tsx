"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { FC, useState, useTransition } from "react";
import ChatBox from "./chat-box";
import { BicepsFlexed, Code, Copy, Folder } from "lucide-react";
import { cn } from "@/lib/utils";
import DotLoader from "@/components/loaders/dot-loader";
import { CreateFormRequest } from "@/lib/form-types";
import { redirect } from "next/navigation";
import { Result } from "@/lib/result";
import { createFormAction } from "@/features/forms/application/actions/create-form.action";

type CreateFormOption =
  | "from_scratch"
  | "from_existing"
  | "from_template"
  | "from_json"
  | "via_assistant";

interface FormCreateSheetProps {
  title: string;
  description: string;
  icon: React.ElementType;
  action?: CreateFormOption;
  isSelected?: boolean;
  disabled?: boolean;
  onClick?: (event: React.MouseEvent<HTMLElement>) => void;
}

const CreateFormCard: FC<FormCreateSheetProps> = ({
  title,
  description,
  icon: Icon,
  onClick,
  isSelected,
  disabled,
}) => {
  return (
    <Card
      onClick={disabled ? undefined : onClick}
      className={cn(
        "hover:border-primary hover:bg-accent focus:outline focus:outline-2 focus:outline-primary-500 flex flex-col overflow-hidden",
        !disabled && "cursor-pointer",
        isSelected && "border-primary bg-accent",
        disabled && "opacity-50 cursor-not-allowed hover:border-border hover:bg-background"
      )}
    >
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-2 p-4 flex-grow">
        <CardTitle className="text-lg font-medium leading-tight">{title}</CardTitle>
        <Icon className="h-8 w-8 text-muted-foreground shrink-0 ml-4" />
      </CardHeader>
      <CardContent className="p-4 bg-muted mt-auto border-t rounded-b-lg">
        <p className="text-xs text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
};

const CreateFormSheet = () => {
  const [pending, setPending] = useState(false);
  const [selectedOption, setSelectedOption] = useState<CreateFormOption>();
  const [isPending, startTransition] = useTransition()
  
  const openNewFormInEditor = async () => {
    if (isPending) {
      return;
    }
    
    startTransition(async () => {
        const request: CreateFormRequest = {
            name: "New Form",
            isEnabled: true,
            formDefinitionJsonData: JSON.stringify("{ }")
        }
        const formResult = await createFormAction(request);
        if (Result.isSuccess(formResult) && formResult.value) {
            const formId = formResult.value;
            redirect(`/forms/${formId}`);
        } else {
            alert("Failed to create form");
        }
    });
  }

  return (
    <SheetContent className="w-[600px] sm:w-[480px] sm:max-w-none flex flex-col h-screen justify-between">
      <SheetHeader className="mb-12">
        <SheetTitle>Create a Form</SheetTitle>
        <SheetDescription>
          Choose one of the following options to create a form.
        </SheetDescription>
      </SheetHeader>
      <div className="flex flex-wrap items-start justify-center flex-grow">
        <div className="grid grid-cols-2 gap-6">
          <CreateFormCard
            title="Start from Scratch"
            description="Use the WYSIWYG Survey Creator to build your form."
            icon={BicepsFlexed}
            action="from_scratch"
            isSelected={selectedOption === "from_scratch"}
            onClick={openNewFormInEditor}
            disabled={isPending}
          />
          <CreateFormCard
            title="Copy an Existing Form"
            description="You have your JSON code ready? Paste it here."
            icon={Copy}
            action="from_existing"
            isSelected={selectedOption === "from_existing"}
            onClick={() => setSelectedOption("from_existing")}
            disabled
          />
          <CreateFormCard
            title="Create from a Template"
            description="Choose from a variety of templates to get started."
            icon={Folder}
            action="from_template"
            isSelected={selectedOption === "from_template"}
            onClick={() => setSelectedOption("from_template")}
            disabled
          />
          <CreateFormCard
            title="Import a Form"
            description="You have your JSON code ready? Paste it here."
            icon={Code}
            action="from_json"
            isSelected={selectedOption === "from_json"}
            onClick={() => setSelectedOption("from_json")}
            disabled
          />
          {/* <CreateFormCard
            title="Use our AI Form Assistant"
            description="The recommended way to create a form."
            icon={Atom}
            action="via_assistant"
            isSelected={selectedOption === "via_assistant"}
            onClick={(event) => {
              setSelectedOption("via_assistant");
              showComingSoonMessage(event);
            }}
          /> */}
        </div>
      </div>
      {pending && <DotLoader className="flex-1 text-center m-auto" />}
      <SheetFooter className="flex-end">
        {selectedOption === "via_assistant" && (
          <ChatBox
            requiresNewContext={false}
            onPendingChange={(pending) => {
              setPending(pending);
            }}
          />
        )}
      </SheetFooter>
    </SheetContent>
  );
};

export default CreateFormSheet;
