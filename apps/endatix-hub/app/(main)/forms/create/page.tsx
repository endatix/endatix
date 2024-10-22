'use client'

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable';
import { NextPage } from 'next';
import ChatBox from '../ui/chat-box';
import PreviewFormContainer from './ui/preview-form-container';
import PageTitle from '@/components/headings/page-title';
import { Atom } from 'lucide-react';
import ChatThread from './ui/chat-thread';
import { useEffect, useState } from 'react';
import { AssistantStore, DefineFormCommand, Message } from '@/lib/use-cases/assistant';
import DotLoader from '@/components/loaders/dot-loader';

const CreateForm: NextPage = () => {

    const [isWaiting, setIsWaiting] = useState(false);
    const [messages, setMessages] = useState(new Array<Message>());
    const [formModel, setFormModel] = useState<any>({});

    useEffect(() => {
        var contextStore = new AssistantStore();
        var currentContext = contextStore.getChatContext();
        if (currentContext?.messages) {
            setMessages(currentContext.messages);
        }

        defineFormHandler(DefineFormCommand.fullStateUpdate);
    }, []);

    const defineFormHandler = (stateCommand: DefineFormCommand) => {
        var contextStore = new AssistantStore();
        switch (stateCommand) {
            case DefineFormCommand.fullStateUpdate:
                const formContext = contextStore.getChatContext();
                const formModel = contextStore.getFormModel();
                    (formModel);
                setMessages(formContext.messages);
                setFormModel(formModel);
                break;
            default:
                break;
        }
    }

    return (
        <div>
            <ResizablePanelGroup direction="horizontal" className="flex flex-1 space-y-2">
                <ResizablePanel defaultSize={65}>
                    <div className="flex h-screen p-8">
                        {formModel && <PreviewFormContainer model={formModel} />}
                    </div>
                </ResizablePanel>
                <ResizableHandle withHandle />
                <ResizablePanel defaultSize={35} minSize={20}>
                    <div className="flex flex-col h-screen z-50 gap-4 bg-background p-6 border-l">
                        <div className="flex items-center gap-2">
                            <PageTitle title="Create your form" />
                            <Atom className="h-8 w-8 text-muted-foreground" />
                        </div>
                        <ChatThread messages={messages} />
                        {isWaiting && <DotLoader className="flex flex-none items-center m-auto" />}
                        <ChatBox
                            className="flex-end flex-none"
                            placeholder="Ask folloup (⌘+F), ↑ to select"
                            onPendingChange={(pending) => { setIsWaiting(pending); }}
                            onStateChange={(stateCommand) => { defineFormHandler(stateCommand); }}
                        />
                    </div>
                </ResizablePanel>
            </ResizablePanelGroup>
        </div>
    );
};

export default CreateForm;
