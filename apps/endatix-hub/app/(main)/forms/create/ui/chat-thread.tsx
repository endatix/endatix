'use client';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Message } from '@/lib/use-cases/assistant';
import { Pencil } from 'lucide-react';
import React from 'react';

interface ChatThreadProps {
    messages: Message[];
}

const ChatThread: React.FC<ChatThreadProps> = ({ messages }) => {
    return (
        <ScrollArea className="h-full p-4">
            {messages.map((message) => (
                <div key={message.content} className={`flex ${message.isAi ? 'justify-start' : 'justify-end'} mb-4`}>
                    <div className={`flex items-start gap-2 max-w-[90%] ${message.isAi ? 'flex-row' : 'flex-row-reverse'}`}>
                        <Avatar className="w-8 h-8">
                            <AvatarImage src={message.isAi ? '/placeholder.svg?height=32&width=32' : '/placeholder-user.jpg'} />
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

