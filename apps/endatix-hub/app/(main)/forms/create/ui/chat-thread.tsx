'use client';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import UserAvatar from '@/components/user/user-avatar';
import { Message } from '@/lib/use-cases/assistant';
import { Pencil } from 'lucide-react';
import React, { useEffect, useRef } from 'react';

interface ChatThreadProps {
    messages: Message[];
    isTyping: boolean;
}

const ChatThread: React.FC<ChatThreadProps> = ({ messages, isTyping }) => {
    const lastMessageRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const timeout = setTimeout(() => {
            if (!isTyping && messages.length > 0 && lastMessageRef.current) {
                lastMessageRef.current.scrollIntoView({ behavior: "smooth" });
            }
        }, 500);

        return () => clearTimeout(timeout);
    }, [messages, isTyping])

    const scrollToLastMessage = () => {
        if (isTyping && lastMessageRef.current) {
            lastMessageRef.current.scrollIntoView({ behavior: "smooth" });
        }
    }

    return (
        <ScrollArea className="relative h-full p-4">
            {messages.map((message, index) => (
                <div key={index}
                    className={`flex relative ${message.isAi ? 'justify-start' : 'justify-end'} mb-4`}
                    ref={index === messages.length - 1 ? lastMessageRef : null}
                >
                    <div className={`flex items-start gap-2 max-w-[90%] ${message.isAi ? 'flex-row' : 'flex-row-reverse'}`}>
                        {message.isAi ?
                            <Avatar className="w-12 h-12 p-2 bg-muted">
                                <AvatarImage className="h-10 p-1 pb-2.5 opacity-50" src={'/icons/atom.svg?height=16&width=16'} />
                                <AvatarFallback>AI</AvatarFallback>
                            </Avatar> :
                            <UserAvatar className="w-10 h-10 bg-muted" isLoggedIn={true} userName={"endatix"} />
                        }
                        <div className={`flex p-3 rounded-lg ${message.isAi ? 'bg-secondary' : 'bg-blue-100 dark:bg-blue-900'}`}>
                            {message.isAi ? (
                                <TypingEffect shouldAddEffect={isTyping && message.isAi && index === messages.length - 1} content={message.content} onNewWordTyped={() => scrollToLastMessage()} />
                            ) : (
                                <p className="line-height-sm">{message.content}</p>
                            )}
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


interface TypingEffectProps {
    content: string,
    shouldAddEffect: boolean,
    onNewWordTyped?: () => void
}
// TypingEffect component implementation
const TypingEffect: React.FC<TypingEffectProps> = ({ content, onNewWordTyped, shouldAddEffect }) => {
    const [text, setText] = React.useState('');
    const [index, setIndex] = React.useState(0);

    const typingSpeed = 10 + Math.floor(Math.random() * 40);

    React.useEffect(() => {
        if (!shouldAddEffect) {
            setText(content);
            return;
        }

        const interval = setInterval(() => {
            setText(content.slice(0, index + 1));
            setIndex(index + 1);
            if (onNewWordTyped) {
                onNewWordTyped();
            }
        }, typingSpeed);

        return () => clearInterval(interval);
    }, [content, index]);

    return <p>{text}</p>;
};
