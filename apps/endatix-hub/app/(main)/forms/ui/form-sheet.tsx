"use client";

import { Button } from "@/components/ui/button"
import { ArrowRight, Eye, Pencil, Trash } from 'lucide-react';
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
import { cn } from "@/lib/utils";
import { Separator } from "@/components/ui/separator";
import { toast } from "sonner";

type FormSheetProps = {
    selectedForm: Form | null
}

const FormSheet = ({ selectedForm }: FormSheetProps) => {
    const getFormattedDate = (date?: Date) => {
        if (!date) {
            return;
        }

        return new Date(date).toLocaleString('en-US', { hour: '2-digit', minute: '2-digit', month: '2-digit', day: '2-digit', year: 'numeric', hour12: true });
    }

    const getFormatDescription = (): string => {
        return selectedForm?.description ?? "This ensures that the first <span> is treated as an inline-block element, which helps maintain its visibility and allows it to occupy space even if its height or width is small. You can also consider adding a min-w or min-h class if you want to enforce a specific size.";
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

    return (
        selectedForm && (
            <Sheet modal={false} open={selectedForm != null} >
                <SheetContent className="w-[600px] sm:w-[480px] sm:max-w-none">
                    <SheetHeader>
                        <SheetTitle>
                            {selectedForm?.name}
                        </SheetTitle>
                        <SheetDescription>
                            {selectedForm?.description}
                        </SheetDescription>
                    </SheetHeader>
                    <div className="my-8 flex space-x-2">
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
                        <Link href="#" onClick={() => toast("Coming soon")}>
                            <Button variant={"outline"}>
                                <Trash className="mr-2 h-4 w-4" />
                                Delete
                            </Button>
                        </Link>
                    </div>
                    <div className="grid gap-2 py-4">
                        <div className="grid grid-cols-4 py-2 items-start gap-4">
                            <span className="text-right self-start">
                                Description
                            </span>
                            <p className="text-sm text-muted-foreground col-span-3">
                                {getFormatDescription()}
                            </p>
                        </div>
                        <div className="grid grid-cols-4 py-2 items-center gap-4">
                            <span className="text-right self-start">
                                Status
                            </span>
                            <div className="col-span-3">
                                <span className={cn("inline-block h-2 w-2 mr-1 rounded-full", selectedForm.isEnabled ? "bg-green-600" : "bg-gray-600")} />
                                <span className="text-sm text-muted-foreground">
                                    {selectedForm.isEnabled ? "Enabled" : "Disabled"}
                                </span>
                            </div>
                        </div>
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
                        <Separator />
                        <div className="grid grid-cols-4 py-2 items-center gap-4">
                            <span className="text-right self-start">
                                Submissions
                            </span>
                            <div className="text-sm text-muted-foreground col-span-3">
                                {getSubmissionsLabel()}
                            </div>
                        </div>
                        {hasSubmissions() === true && (
                            <div>
                                <Link href={`/forms/submissions/${selectedForm.id}`}>
                                    <Button variant={"ghost"}>
                                        <ArrowRight className="mr-2 h-4 w-4" />
                                        View Submissions
                                    </Button>
                                </Link>
                            </div>
                        )}
                    </div>
                    <SheetFooter>
                    </SheetFooter>
                </SheetContent>
            </Sheet>
        )
    );
}

export default FormSheet;

