import { Message } from '@/lib/use-cases/assistant';
import React, { createContext, useState, useContext, ReactNode } from 'react';

interface ChatContextType {
    messages: Message[];
    addMessage: (message: Message) => void;
}

const ChatContext = createContext<ChatContextType | undefined>(undefined);

export const ChatProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
    const [messages, setMessages] = useState<Message[]>(new Array<Message>());

    const addMessage = (message: Message) => {
        setMessages((prevMessages) => [...prevMessages, message]);
    };

    return (
        <ChatContext value={{ messages, addMessage }}>
            {children}
        </ChatContext>
    );
};

export const useChat = (): ChatContextType => {
    const context = useContext(ChatContext);
    if (!context) {
        throw new Error('useChat must be used within a ChatProvider');
    }
    return context;
};