"use client";

import { Button } from "@/components/ui/button"
import { Copy, Link2, List, Pencil } from 'lucide-react';
import {
    Sheet,
    SheetContent,
    SheetDescription,
    SheetFooter,
    SheetHeader,
    SheetTitle,
} from "@/components/ui/sheet";
import { Form } from "@/types";
import Link from "next/link";
import { SectionTitle } from "@/components/headings/section-title";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { useTransition, useState } from "react";
import { updateFormStatusAction } from "../[formId]/update-form-status.action";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";

type FormSheetProps = {
    selectedForm: Form | null,
    enableEditing?: boolean
}

const FormSheet = ({
    selectedForm,
    enableEditing = false
}: FormSheetProps) => {
    const [pending, startTransition] = useTransition();
    const [isEnabled, setIsEnabled] = useState(selectedForm?.isEnabled);

    if (!selectedForm) {
        return null;
    }

    const getFormattedDate = (date?: Date) => {
        if (!date) {
            return;
        }

        return new Date(date).toLocaleString('en-US', { hour: '2-digit', minute: '2-digit', month: '2-digit', day: '2-digit', year: 'numeric', hour12: true });
    }

    const getSubmissionsLabel = () => {
        const count = selectedForm?.submissionsCount ?? 0;
        if (count === 0) {
            return "No submissions yet";
        }

        return `${count}`;
    }

    const enabledLabel = selectedForm?.isEnabled ? "Enabled" : "Disabled";

    const toggleEnabled = async (enabled: boolean) => {
        setIsEnabled(enabled)
        startTransition(async () => {
            try {
                await updateFormStatusAction(selectedForm.id, enabled);
                toast(`Form is now ${enabled ? "enabled" : "disabled"}`);
            } catch (error) {
                setIsEnabled(!enabled);
                toast.error("Failed to update form status. Error: " + error);
            }
        });
    }

    const copyToClipboard = (value: string) => {
        navigator.clipboard.writeText(value);
        toast("Copied to clipboard")
    }

    return (
        selectedForm && (
            <Sheet modal={false} open={selectedForm != null} >
                <SheetContent className="w-[600px] sm:w-[480px] sm:max-w-none">
                    <SheetHeader>
                        <SheetTitle className="text-2xl font-bold">
                            {selectedForm?.name}
                        </SheetTitle>
                        <SheetDescription>
                            {selectedForm?.description}
                        </SheetDescription>
                    </SheetHeader>
                    <div className="my-8 flex space-x-2 justify-end">
                        <Button variant={"outline"} asChild>
                            <Link href={`forms/${selectedForm.id}`}>
                                <Pencil className="mr-2 h-4 w-4" />
                                Design
                            </Link>
                        </Button>
                        <Button variant={"outline"} asChild>
                            <Link href={`share/${selectedForm.id}`}>

                                <Link2 className="mr-2 h-4 w-4" />
                                Share
                            </Link>
                        </Button>

                        <Button variant={"outline"} asChild>
                            <Link href={`forms/${selectedForm.id}/submissions`}>
                                <List className="w-4 h-4 mr-1" />
                                Submissions
                            </Link>
                        </Button>

                    </div>
                    <div className="grid gap-2 py-4">
                        <div className="grid grid-cols-4 py-2 items-center gap-4">
                            <span className="text-right self-start">
                                Created on
                            </span>
                            <span className="text-sm text-muted-foreground col-span-3">
                                {getFormattedDate(selectedForm.createdAt)}
                            </span>
                        </div>
                        <div className="grid grid-cols-4 py-2 items-center gap-4">
                            <span className="text-right self-start">
                                Modified  on
                            </span>
                            <span className="text-sm text-muted-foreground col-span-3">
                                {getFormattedDate(selectedForm.modifiedAt)}
                            </span>
                        </div>

                        <div className="grid grid-cols-4 py-2 items-center gap-4">
                            <span className="text-right self-start">
                                Status
                            </span>
                            <div className="col-span-3 flex items-center space-x-2">
                                {enableEditing ? (
                                    <>
                                        <Switch
                                            id="form-status"
                                            checked={isEnabled}
                                            onCheckedChange={toggleEnabled}
                                            disabled={pending}
                                            aria-readonly />
                                        <Label htmlFor="form-status">
                                            {enabledLabel}
                                        </Label>
                                    </>

                                ) : (
                                    <Badge variant={selectedForm.isEnabled ? "default" : "secondary"}>
                                        {enabledLabel}
                                    </Badge>
                                )}
                            </div>
                        </div>

                        <div className="grid grid-cols-4 py-2 items-center gap-4">
                            <span className="col-span-1 text-right self-start">
                                Submissions
                            </span>
                            <div className="text-sm text-muted-foreground col-span-3">
                                {getSubmissionsLabel()}
                            </div>
                        </div>
                    </div>

                    <SectionTitle title="Sharing" headingClassName="text-xl mt-4" />
                    <div className="grid gap-2">
                        <div className="grid grid-cols-4 py-2 items-center gap-4">
                            <span className="text-right self-start">
                                Default Url:
                            </span>
                            <div className="text-sm text-muted-foreground col-span-3">
                                <div className="relative" onClick={() => copyToClipboard(`/share/${selectedForm.id}`)}>
                                    <div className="absolute right-2.5 top-2.5 h-4 w-4 text-muted-foreground">
                                        <Copy className="h-4 w-4 cursor-pointer" />
                                    </div>
                                    <Input
                                        readOnly
                                        id="form-share-url"
                                        value={`/share/${selectedForm.id}`}
                                        className="bg-accent w-full rounded-lg pl-8"
                                    />
                                </div>

                            </div>
                        </div>
                    </div>
                    <SheetFooter>
                    </SheetFooter>
                </SheetContent >
            </Sheet >
        )
    );
}

export default FormSheet;