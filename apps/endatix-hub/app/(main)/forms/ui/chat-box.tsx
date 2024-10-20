"use client"

import { AlertCircle, CornerDownLeft, Mic, Paperclip } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import {
    Tooltip,
    TooltipContent,
    TooltipProvider,
    TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"
import { defineFormAction } from "../define-form.action"
import { useActionState } from "react"
import { useFormStatus } from "react-dom"
import { DefineFormResult, IDefineFormResult } from "../define-form-result"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"

interface ChatBoxProps {
}

const initialState: IDefineFormResult = DefineFormResult.InitialState();

const SubmitButton = () => {
    const { pending } = useFormStatus();
    return (
        <Button type="submit" size="sm" className={cn("ml-auto gap-1.5", pending ? "opacity-50 cursor-progress" : "")} aria-disabled={pending} disabled={pending}>
            {pending ? "..." : "Chat"}
            <CornerDownLeft className="size-3.5" />
        </Button>
    );
}

const ChatErrorAlert = ({ errorMessage }: { errorMessage: string | undefined }) => {
    return (
        <Alert variant="destructive" className="">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Error</AlertTitle>
            <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
    );
}

const ChatBox = () => {
    const [state, action] = useActionState(defineFormAction, initialState);

    return (
        <div className="flex flex-col flex-1 gap-2">
            {state.success === false && <ChatErrorAlert errorMessage={state.errorMessage} />}
            <form
                action={action}
                className="flex-1 relative overflow-hidden rounded-lg border bg-background focus-within:ring-1 focus-within:ring-ring"
            >
                <Label htmlFor="prompt" className="sr-only">
                    Your prompt here
                </Label>
                <Textarea
                    id="prompt"
                    name="prompt"
                    placeholder="What would you like to achieve with your form?"
                    className="min-h-12 resize-none border-0 p-3 shadow-none focus-visible:ring-0"
                />
                <div className="flex items-center p-3 pt-0">
                    <TooltipProvider>
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button disabled variant="ghost" size="icon" className="disabled:opacity-50">
                                    <Paperclip className="size-4" />
                                    <span className="sr-only">Attach file</span>
                                </Button>
                            </TooltipTrigger>
                            <TooltipContent side="top">Attach File</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button disabled variant="ghost" size="icon" className="disabled:opacity-50">
                                    <Mic className="size-4" />
                                    <span className="sr-only">Use Microphone</span>
                                </Button>
                            </TooltipTrigger>
                            <TooltipContent side="top">Use Microphone</TooltipContent>
                        </Tooltip>
                    </TooltipProvider>
                    <SubmitButton />
                </div>
            </form>
        </div>
    )
}

export default ChatBox;