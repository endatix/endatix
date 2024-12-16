"use client";

import { Button } from "@/components/ui/button"
import { ArrowRight, Eye, Pencil } from 'lucide-react';
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

type FormSheetProps = {
    selectedForm: Form | null,
    onFormUpdate: (updatedForm: Form) => Promise<void>;
}

const FormSheet = ({
    selectedForm,
    onFormUpdate }: FormSheetProps) => {
    const [pending, startTransition] = useTransition();
    const [isEnabled, setIsEnabled] = useState(selectedForm?.isEnabled ?? false);

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

    const hasSubmissions = () => {
        return (selectedForm?.submissionsCount && selectedForm.submissionsCount > 0) ?? false;
    }

    const toggleEnabled = async (enabled: boolean) => {
        setIsEnabled(enabled); // Optimistically update UI
        startTransition(async () => {
            try {
                await updateFormStatusAction(selectedForm.id, enabled);
                await onFormUpdate(selectedForm);
                toast(`Form is now ${enabled ? "enabled" : "disabled"}`);
            } catch (error) {
                setIsEnabled(!enabled);
                toast.error("Failed to update form status");
            }
        });
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
                        <Link href={`forms/${selectedForm.id}`}>
                            <Button variant={"outline"}>
                                <Pencil className="mr-2 h-4 w-4" />
                                Edit
                            </Button>
                        </Link>
                        <Link href={`share/${selectedForm.id}`}>
                            <Button variant={"outline"}>
                                <Eye className="mr-2 h-4 w-4" />
                                Preview
                            </Button>
                        </Link>
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
                                <Switch
                                    id="form-status"
                                    checked={isEnabled}
                                    onCheckedChange={toggleEnabled}
                                    disabled={pending}
                                    aria-readonly />
                                <Label htmlFor="form-status">
                                    {isEnabled ? "Enabled" : "Disabled"}
                                </Label>
                            </div>
                        </div>
                    </div>
                    <SectionTitle title="Sharing" headingClassName="text-xl" />
                    <SectionTitle title="Submissions" headingClassName="text-xl" />
                    {hasSubmissions() === true && (
                        <div>
                            <Link href={`/forms/${selectedForm.id}/submissions`}>
                                <Button variant={"ghost"}>
                                    <ArrowRight className="mr-2 h-4 w-4" />
                                    View Submissions
                                </Button>
                            </Link>
                        </div>
                    )}
                    <SheetFooter>
                    </SheetFooter>
                </SheetContent>
            </Sheet>
        )
    );
}

export default FormSheet;
