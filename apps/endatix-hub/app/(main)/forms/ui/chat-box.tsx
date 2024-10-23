'use client'

import { AlertCircle, CornerDownLeft, Mic, Paperclip, StopCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
    Tooltip,
    TooltipContent,
    TooltipProvider,
    TooltipTrigger,
} from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import { defineFormAction } from '../define-form.action'
import { useActionState, useEffect } from 'react'
import { IPromptResult, PromptResult } from '../prompt-result'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { AssistantStore, DefineFormCommand } from '@/lib/use-cases/assistant'
import { redirect } from 'next/navigation'

const ChatErrorAlert = ({ errorMessage }: { errorMessage: string | undefined }) => {
    return (
        <Alert variant='destructive' className=''>
            <AlertCircle className='h-4 w-4' />
            <AlertTitle>Error</AlertTitle>
            <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
    );
}

const SubmitButton = ({ pending }: { pending: boolean }) => {
    return (
        <Button type='submit' size='sm' className={cn('ml-auto gap-1.5 w-24', (pending ? 'cursor-progress' : ''))} aria-disabled={pending} disabled={pending}>
            Chat
            {pending ? <StopCircle className='size-6' /> : <CornerDownLeft className='size-3' />}
        </Button>
    );
}

const initialState = PromptResult.InitialState();

interface ChatBoxProps extends React.HTMLAttributes<HTMLDivElement> {
    requiresNewContext?: boolean;
    placeholder?: string;
    onPendingChange?: (pending: boolean) => void;
    onStateChange?: (stateCommand: DefineFormCommand) => void;
}

const ChatBox = ({ className, placeholder, requiresNewContext, onPendingChange, onStateChange, ...props }: ChatBoxProps) => {
    const [state, action, pending] = useActionState(
        async (prevState: IPromptResult, formData: FormData) => {
            var contextStore = new AssistantStore();

            if (requiresNewContext) {
                contextStore.clear();
            }

            const formContext = contextStore.getChatContext();
            if (formContext) {
                formData.set("threadId", formContext.threadId ?? '');
                formData.set("assistantId", formContext.assistantId ?? '');
            }

            const promptResult = await defineFormAction(prevState, formData);

            if (promptResult.success && promptResult.value?.definition) {
                var prompt = formData.get("prompt") as string;
                contextStore.setFormModel(promptResult.value?.definition);

                var currentContext = contextStore.getChatContext();
                if (!currentContext) {
                    currentContext = {
                        messages: [],
                        threadId: promptResult.value?.threadId ?? '',
                        assistantId: promptResult.value?.assistantId ?? ''
                    }
                }

                if (currentContext.messages === undefined) {
                    currentContext.messages = [];
                }

                currentContext.messages.push({
                    isAi: false,
                    content: prompt
                });

                if (promptResult.value?.assistantResponse) {
                    currentContext.messages.push({
                        isAi: true,
                        content: promptResult.value?.assistantResponse
                    });
                }

                contextStore.setChatContext(currentContext);

                if (onStateChange) {
                    onStateChange(DefineFormCommand.fullStateUpdate);
                }

                if (window.location.pathname === '/forms') {
                    redirect('/forms/create');
                }
            }
            return promptResult;
        }, initialState);

    useEffect(() => {
        if (onPendingChange) {
            onPendingChange(pending);
        }
    }, [pending, onPendingChange]);

    return (
        <div className={`flex flex-col flex-1 gap-2 ${className}`} {...props}>
            {state.success === false && <ChatErrorAlert errorMessage={state.errorMessage} />}
            <form
                action={action}
                className='flex-1 relative overflow-hidden rounded-lg border bg-background focus-within:ring-1 focus-within:ring-ring'>
                <Label htmlFor='prompt' className='sr-only'>
                    Your prompt here
                </Label>
                <Textarea
                    id='prompt'
                    name='prompt'
                    placeholder={placeholder ?? 'What would you like to achieve with your form?'}
                    className='min-h-12 resize-none border-0 p-3 shadow-none focus:outline-none focus-visible:ring-0'
                />
                <div className='flex items-center p-3 pt-0'>
                    <TooltipProvider>
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button disabled variant='ghost' size='icon' className='disabled:opacity-50'>
                                    <Paperclip className='size-4' />
                                    <span className='sr-only'>Attach file</span>
                                </Button>
                            </TooltipTrigger>
                            <TooltipContent side='top'>Attach File</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button disabled variant='ghost' size='icon' className='disabled:opacity-50'>
                                    <Mic className='size-4' />
                                    <span className='sr-only'>Use Microphone</span>
                                </Button>
                            </TooltipTrigger>
                            <TooltipContent side='top'>Use Microphone</TooltipContent>
                        </Tooltip>
                    </TooltipProvider>
                    <SubmitButton pending={pending} />
                </div>
            </form>
        </div>
    )
}

export default ChatBox;