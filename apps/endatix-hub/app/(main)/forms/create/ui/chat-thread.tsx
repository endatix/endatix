'use client';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Message } from '@/lib/use-cases/assistant';
import { Pencil } from 'lucide-react';
import React, { useEffect } from 'react';

interface ChatThreadProps {
    messages: Message[];
}

const ChatThread: React.FC<ChatThreadProps> = ({ messages }) => {
    useEffect(() => {

    }, [messages]);

    return (
        <ScrollArea className="h-full p-4">
            {messages.map((message) => (
                <div key={message.content} className={`flex ${message.isAi ? 'justify-start' : 'justify-end'} mb-4`}>
                    <div className={`flex items-start gap-2 max-w-[90%] ${message.isAi ? 'flex-row' : 'flex-row-reverse'}`}>
                        <Avatar className="w-12 h-12 p-2 bg-muted">
                            <AvatarImage className="h-10 p-1 pb-2.5 opacity-50" src={message.isAi ? '/icons/atom.svg?height=16&width=16' : '/icons/user.svg?height=16&width=16'} />
                            <AvatarFallback>{message.isAi ? 'AI' : 'You'}</AvatarFallback>
                        </Avatar>
                        <div className={`flex p-3 rounded-lg ${
                            message.isAi ? 'bg-secondary' : 'bg-blue-100 dark:bg-blue-900'}`}>
                            <p className="line-height-sm">{message.content}</p>
                            {!message.isAi && (
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="ml-2"
                                    // onClick={() => handleEditPrompt(message.id)}
                                >
                                    <Pencil className="h-4 w-4 ml-auto flex-end" />
                                    <span className="sr-only">Edit prompt</span>
                                </Button>
                            )}
                        </div>
                    </div>
                </div>
            ))}
        </ScrollArea>
    );
};

export default ChatThread;

