'use client'

import { CornerDownLeft, Mic, Paperclip, StopCircle } from 'lucide-react'
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
import { useState } from 'react'

const SubmitButton = ({ pending, disabled }: { pending: boolean, disabled: boolean }) => {
    return (
        <Button type='submit' size='sm' className={cn('ml-auto gap-1.5 w-24', (pending ? 'cursor-progress' : ''))} aria-disabled={pending} disabled={disabled || pending}>
            Chat
            {pending ? <StopCircle className='size-6' /> : <CornerDownLeft className='size-3' />}
        </Button>
    );
}

interface ChatBoxProps extends React.HTMLAttributes<HTMLDivElement> {
    requiresNewContext?: boolean;
    placeholder?: string;
    onPendingChange?: (pending: boolean) => void;
    onStateChange?: () => void;
}

const ChatBox = ({ className, placeholder, ...props }: ChatBoxProps) => {
    const [input, setInput] = useState('')

    return (
        <div className={`flex flex-col flex-1 gap-2 ${className}`} {...props}>
            <form
                className='flex-1 relative overflow-hidden rounded-lg border bg-background focus-within:ring-1 focus-within:ring-ring'>
                <Label htmlFor='prompt' className='sr-only'>
                    Your prompt here
                </Label>
                <Textarea
                    id='prompt'
                    name='prompt'
                    disabled
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
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
                    <SubmitButton pending={false} disabled={input.length === 0} />
                </div>
            </form>
            <p className="text-center text-xs text-gray-500">Endatix may make mistakes. Please use with discretion.</p>
        </div>
    )
}

export default ChatBox;