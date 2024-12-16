import PageTitle from "@/components/headings/page-title";
import { Button } from "@/components/ui/button";
import {
    Download,
    FilePenLine,
    LinkIcon,
    Sparkles,
    Trash2
} from "lucide-react";
import Link from "next/link";
import { SubmissionActionsDropdown } from "./submission-actions-dropdown";
import { Submission } from "@/types";

interface SubmissionHeaderProps {
    submission: Submission;
}

export function SubmissionHeader({
    submission
}: SubmissionHeaderProps) {
    return (
        <div className="my-2 flex flex-col gap-6 sm:gap-2 sm:flex-row justify-between">
            <PageTitle title="Submission Details" />
            <div className="flex space-x-2 justify-end text-muted-foreground">
                <Link href="#">
                    <Button variant={"outline"}>
                        <Download className="mr-2 h-4 w-4" />
                        Export PDF
                    </Button>
                </Link>
                <Link href="#">
                    <Button variant={"outline"}>
                        <LinkIcon className="mr-2 h-4 w-4" />
                        Share Link
                    </Button>
                </Link>
                <Link href="#" className="hidden">
                    <Button variant={"outline"}>
                        <FilePenLine className="mr-2 h-4 w-4" />
                        Edit
                    </Button>
                </Link>
                <Link href="#" className="hidden">
                    <Button variant={"outline"}>
                        <Sparkles className="mr-2 h-4 w-4" />
                        Mark as new
                    </Button>
                </Link>
                <Link href="#" className="hidden">
                    <Button variant={"outline"}>
                        <Trash2 className="mr-2 h-4 w-4" />
                        Delete
                    </Button>
                </Link>

                <SubmissionActionsDropdown
                    submission={submission}
                    className="text-muted-foreground"
                />
            </div>
        </div>
    )
}