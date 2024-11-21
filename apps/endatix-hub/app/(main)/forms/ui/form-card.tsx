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
import { ArrowRight, Download, EyeIcon, Globe, Link2, Lock } from "lucide-react";
import React from "react";
import { Button } from "@/components/ui/button";

type FormCardProps = React.ComponentProps<typeof Card> & {
  form: Form,
  isSelected: boolean
}

interface DateLabelProps {
  label: string;
  value: string;
}

interface SubmissionsLabelProps {
  formId: string,
  submissionsCount?: number
}

const DateLabel: React.FC<DateLabelProps> = ({ label, value }) => {
  return (
    <div className="grid grid-cols-7 w-full">
      <div className="col-span-2 text-sm text-muted-foreground">
        {label}:
      </div>
      <div className="col-span-5 text-sm font-medium text-gray-500">
        {value}
      </div>
    </div>
  )
}

const SubmissionsLabel: React.FC<SubmissionsLabelProps> = ({
  submissionsCount = 0,
  formId }) => {

  const submissionWord = submissionsCount === 1 ? "submission" : "submissions";
  const getFormattedSubmissionsCount = () => {
    const dividedByThousand = submissionsCount / 1000;
    if (dividedByThousand > 1) {
      return `${dividedByThousand.toFixed(1)}k`;
    }

    return submissionsCount.toString();
  }

  if (submissionsCount == 0) {
    return <span className="text-sm text-muted-foreground">No submissions yet</span>
  }

  return (
    <Button variant="link" asChild className="group justify-start p-0 inline-block align-baseline hover:no-underline">
      <Link href={`forms/submissions/${formId}`} className="py-2">
        <span className="text-2xl font-medium group-hover:text-primary text-muted-foreground transition-text duration-250 ease-in-out">{getFormattedSubmissionsCount()}</span>
        <span className="pl-2 text-sm group-hover:text-primary text-muted-foreground transition-text duration-250 ease-in-out">{submissionWord}</span>
        <ArrowRight className="mb-0.5 inline-block ml-2 h-4 w-4 opacity-0 transition-opacity duration-250 ease-in-out group-hover:opacity-100" />
      </Link>
    </Button>
  )
}

const FormCard = ({ form, isSelected, className, ...props }: FormCardProps) => {
  const getFormLabel = () => form.isEnabled ? "Enabled" : "Disabled";

  const formatDate = (date: Date) => {
    const dateObj = new Date(date);
    const month = dateObj.getMonth() + 1;
    const day = dateObj.getDate();
    const year = dateObj.getFullYear();
    return `${month < 10 ? '0' : ''}${month}/${day < 10 ? '0' : ''}${day}/${year}`;
  }

  return (
    <Card className={cn("flex cursor-pointer flex-col gap-1 hover:bg-accent justify-between", isSelected ? "bg-accent border-primary" : "", className)} {...props}>
      <CardHeader className="flex flex-row justify-between p-4 pt-6">
        <CardTitle className="text-2xl font-normal font-sans tracking-tigher">{form.name}</CardTitle>
        {form.isEnabled ?
          <Globe className="w-6 h-6 text-muted-foreground" /> :
          <Lock className="w-6 h-6 text-muted-foreground" />
        }
      </CardHeader>
      <CardContent className="grid gap-4 p-4">
        <div className="flex flex-col gap-0">
          <DateLabel label="Created on" value={formatDate(form.createdAt)} />
          <DateLabel label="Expires" value="never" />
        </div>

        <SubmissionsLabel
          formId={form.id}
          submissionsCount={form.submissionsCount} />
      </CardContent>
      <CardFooter className="pb-2 p-4">
        <div className="flex justify-between w-full">
          <div className="flex items-center gap-4">
            <Link href="#" className="text-sm text-muted-foreground hover:text-foreground inline-flex items-center">
              <Link2 className="w-4 h-4 mr-1" />
              Share
            </Link>
            <Link href={`/share/${form.id}`} target="_blank" className="text-sm text-muted-foreground hover:text-foreground inline-flex items-center">
              <EyeIcon className="w-4 h-4 mr-1" />
              Preview
            </Link>
            <Link href="#" className="text-sm text-muted-foreground hover:text-foreground inline-flex items-center">
              <Download className="w-4 h-4 mr-1" />
              Export
            </Link>
          </div>
          <div className="flex ml-auto text-xs text-foreground">
            <Badge className="text-xs font-normal" variant={form.isEnabled ? "default" : "secondary"}>
              {getFormLabel()}
            </Badge>
          </div>
        </div>
      </CardFooter>
    </Card>
  )
}

export default FormCard;