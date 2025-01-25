'use client'

import { Button } from "@/components/ui/button"
import { Copy, Link2, List, Pencil, MoreHorizontal, Trash2, AlertTriangle } from 'lucide-react';
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
import { updateFormStatusAction } from "../../../app/(main)/forms/[formId]/update-form-status.action";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { deleteFormAction } from "../application/actions/delete-form.action";
import { Result } from "@/lib/result";
import { useRouter } from "next/navigation";

interface FormSheetProps extends React.ComponentPropsWithoutRef<typeof Sheet> {
    selectedForm: Form | null,
    enableEditing?: boolean
}

interface DeleteFormDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    formName: string;
    submissionsCount: number;
    onDelete: () => Promise<void>;
}

const DeleteFormDialog = ({
    isOpen,
    onOpenChange,
    formName,
    submissionsCount,
    onDelete,
}: DeleteFormDialogProps) => {
    const [formNameInput, setFormNameInput] = useState("");

    const handleOpenChange = (open: boolean) => {
        onOpenChange(open);
        if (!open) {
            setFormNameInput("");
        }
    };

    const handleDeleteClick = (e: React.MouseEvent) => {
        e.preventDefault();
        if (formNameInput !== formName) {
            toast.error("Form name doesn't match");
            return;
        }
        onDelete();
    };

    return (
        <AlertDialog open={isOpen} onOpenChange={handleOpenChange}>
            <AlertDialogContent>
                <AlertDialogHeader>
                    <AlertDialogTitle>
                        Are you sure you want to delete form <strong>{formName}</strong>?
                    </AlertDialogTitle>
                    <AlertDialogDescription className="space-y-4 mb-1">
                        <span className="flex items-center gap-2 text-destructive font-medium">
                            <AlertTriangle className="h-4 w-4" />
                            This action will permanently delete the form, all its definitions and submissions, and cannot be undone.
                        </span>
                        <span className="block text-sm">
                            <strong>{formName}</strong> has <strong>{submissionsCount}</strong> submissions.
                        </span>
                        <span className="block text-sm">
                            To confirm, type the name of the form below:
                        </span>
                    </AlertDialogDescription>
                    <Input
                        type="text"
                        placeholder={`Type "${formName}"`}
                        value={formNameInput}
                        onChange={(e) => setFormNameInput(e.target.value)}
                        className="w-full mt-1"
                    />
                </AlertDialogHeader>
                
                <AlertDialogFooter>
                    <AlertDialogCancel>Cancel</AlertDialogCancel>
                    <AlertDialogAction 
                        onClick={handleDeleteClick}
                        className="bg-destructive hover:bg-destructive/90"
                    >
                        Delete Form
                    </AlertDialogAction>
                </AlertDialogFooter>
            </AlertDialogContent>
        </AlertDialog>
    );
};

const FormSheet = ({
    selectedForm,
    enableEditing = false,
    ...props
}: FormSheetProps) => {
    const [pending, startTransition] = useTransition();
    const [isEnabled, setIsEnabled] = useState(selectedForm?.isEnabled);
    const [isDropdownOpen, setIsDropdownOpen] = useState(false);
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const router = useRouter();

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

    const handleDialogOpenChange = (open: boolean) => {
        setIsDialogOpen(open);
    };

    const handleDelete = async () => {
        startTransition(async () => {
            try {
                const result = await deleteFormAction(selectedForm.id);
                if (Result.isSuccess(result)) {
                    toast.success(<>Form <strong>{selectedForm.name}</strong> deleted successfully</>);
                    setIsDialogOpen(false);
                    props.onOpenChange?.(false);
                    setTimeout(() => {
                        router.push('/forms');
                        router.refresh();
                    }, 1000);
                } else {
                    toast.error("Failed to delete form");
                }
            } catch (error) {
                toast.error("Failed to delete form");
            }
        });
    };

    const handleOpenDeleteDialog = () => {
        setIsDropdownOpen(false);
        setIsDialogOpen(true);
    };

    return (
        selectedForm && (
            <Sheet {...props}>
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
                        <DropdownMenu open={isDropdownOpen} onOpenChange={setIsDropdownOpen}>
                            <DropdownMenuTrigger asChild>
                                <Button variant="outline" size="icon">
                                    <MoreHorizontal className="h-4 w-4" />
                                    <span className="sr-only">More options</span>
                                </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                                <DropdownMenuItem
                                    className="text-destructive cursor-pointer"
                                    onClick={handleOpenDeleteDialog}
                                >
                                    <Trash2 className="mr-2 h-4 w-4" />
                                    Delete
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </div>

                    <DeleteFormDialog
                        isOpen={isDialogOpen}
                        onOpenChange={handleDialogOpenChange}
                        formName={selectedForm.name}
                        submissionsCount={selectedForm.submissionsCount || 0}
                        onDelete={handleDelete}
                    />

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
                    <div className="grid grid-cols-4 py-2 gap-4">
                        <div className="col-span-1 flex items-center justify-end">
                            <Label htmlFor="form-share-url">
                                Default Url:
                            </Label>
                        </div>
                        <div className="col-span-3">
                            <div className="relative cursor-pointer">
                                <div className="absolute right-2.5 top-2.5 h-4 w-4 text-muted-foreground cursor-pointer z-10">
                                    <Copy
                                        onClick={() => copyToClipboard(`/share/${selectedForm.id}`)}
                                        aria-label="Copy form url"
                                        className="h-4 w-4" />
                                </div>
                                <Input
                                    readOnly
                                    disabled
                                    id="form-share-url"
                                    value={`/share/${selectedForm.id}`}
                                    className="bg-accent w-full rounded-lg"
                                />
                            </div>
                        </div>
                    </div>
                    <SheetFooter>
                    </SheetFooter>
                </SheetContent>
            </Sheet>
        )
    );
}
export default FormSheet;
