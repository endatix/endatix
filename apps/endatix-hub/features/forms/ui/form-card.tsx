"use client";

import { cn } from "@/lib/utils";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Form } from "@/types";
import { Badge } from "@/components/ui/badge";
import Link from "next/link";
import { Link2, List, Pencil } from "lucide-react";
import React from "react";

type FormCardProps = React.ComponentProps<typeof Card> & {
  form: Form;
  isSelected: boolean;
};

interface SubmissionsLabelProps {
  formId: string;
  submissionsCount?: number;
}

const SubmissionsLabel: React.FC<SubmissionsLabelProps> = ({
  submissionsCount = 0,
}) => {
  const submissionWord = submissionsCount === 1 ? "submission" : "submissions";
  const getFormattedSubmissionsCount = () => {
    const dividedByThousand = submissionsCount / 1000;
    if (dividedByThousand > 1) {
      return `${dividedByThousand.toFixed(1)}k`;
    }

    return submissionsCount.toString();
  };

  if (submissionsCount == 0) {
    return (
      <span className="text-sm text-muted-foreground">No submissions yet</span>
    );
  }

  return (
    <div>
      <span className="text-2xl font-medium text-muted-foreground">
        {getFormattedSubmissionsCount()}
      </span>
      <span className="pl-2 text-sm text-muted-foreground">
        {submissionWord}
      </span>
    </div>
  );
};

const FormCard = ({ form, isSelected, className, ...props }: FormCardProps) => {
  const getFormLabel = () => (form.isEnabled ? "Enabled" : "Disabled");

  return (
    <Card
      className={cn(
        "flex flex-col gap-1 hover:bg-accent justify-between group",
        isSelected ? "bg-accent border-primary" : "",
        className,
      )}
      {...props}
    >
      <div className="cursor-pointer">
        <CardHeader className="flex flex-row justify-between p-4 pt-6">
          <CardTitle className="text-2xl font-normal font-sans tracking-tigher">
            {form.name}
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 p-4">
          <div className="flex justify-between items-center">
            <SubmissionsLabel
              formId={form.id}
              submissionsCount={form?.submissionsCount}
            />
            <Badge
              className="text-xs font-normal pointer-events-none"
              variant={form.isEnabled ? "default" : "secondary"}
            >
              {getFormLabel()}
            </Badge>
          </div>
        </CardContent>
      </div>
      <CardFooter
        className="pb-2 p-4 bg-muted mt-auto border-t rounded-b-lg cursor-default"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-between w-full">
          <div className="flex items-center gap-4 opacity-0 group-hover:opacity-100 transition-opacity">
            <Link
              href={`forms/${form.id}`}
              className="text-sm text-muted-foreground hover:text-foreground inline-flex items-center cursor-pointer"
            >
              <Pencil className="w-4 h-4 mr-1" />
              Design
            </Link>
            <Link
              href={`/share/${form.id}`}
              target="_blank"
              className="text-sm text-muted-foreground hover:text-foreground inline-flex items-center cursor-pointer"
            >
              <Link2 className="w-4 h-4 mr-1" />
              Share
            </Link>
            <Link
              href={
                form?.submissionsCount ? `forms/${form.id}/submissions` : "#"
              }
              className={cn(
                "text-sm text-muted-foreground inline-flex items-center",
                form?.submissionsCount
                  ? "hover:text-foreground cursor-pointer"
                  : "opacity-50 pointer-events-none cursor-default",
              )}
            >
              <List className="w-4 h-4 mr-1" />
              Submissions
            </Link>
          </div>
        </div>
      </CardFooter>
    </Card>
  );
};

export default FormCard;
