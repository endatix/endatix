"use client";

import { cn } from "@/lib/utils";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Form } from "@/types";
import { Badge } from "@/components/ui/badge";
import Link from "next/link";

type FormCardProps = React.ComponentProps<typeof Card> & {
  form: Form
}

const FormCard = ({ form, className, ...props }: FormCardProps) => {
  const getFormLabel = () => form.isEnabled ? "Active" : "Disabled";

  const getSubmissionsLabel = () => {
    const count = form.submissionsCount ?? 0;
    if (count === 0) {
      return "No submissions yet";
    }

    const submissionWord = count === 1? "submission" : "submissions";
    return `${count} ${submissionWord}`;
  }

  return (
    <Card
      onClick={() => { console.log(`Clicked form #${form.id}`) }}
      className={cn("flex cursor-pointer w-full flex-col gap-1 hover:bg-accent", className)} {...props}>
      <CardHeader>
        <div className="flex ml-auto text-xs text-foreground">
          <Badge variant="outline">
            <span className={cn("flex h-2 w-2 mr-1 rounded-full", form.isEnabled ? "bg-green-600" : "bg-gray-600")} />
            {getFormLabel()}
          </Badge>
        </div>
        <CardTitle className="text-xl">{form.name}</CardTitle>
        <CardDescription className="p-1">
          <span className="text-sm font-medium leading-none pr-2">
            Created on:
          </span>
          <span className="text-sm text-muted-foreground">
            {new Date(form.createdAt).toDateString()}
          </span>
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        {/* <div>
          <Progress value={33} />
        </div> */}
      </CardContent>
      <CardFooter>
        <div className="flex items-center gap-2">
          <Link href={`forms/submissions/${form.id}`}>
            <Badge variant="default">
              {getSubmissionsLabel()}
            </Badge>
          </Link>
        </div>
      </CardFooter>
    </Card>
  )
}

export default FormCard;