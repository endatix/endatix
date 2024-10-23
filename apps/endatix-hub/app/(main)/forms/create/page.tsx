'use client'

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable'
import { NextPage } from 'next'
import ChatBox from '../ui/chat-box'
import PreviewFormContainer from './ui/preview-form-container'
import ChatThread from './ui/chat-thread'
import { useEffect, useRef, useState } from 'react'
import { AssistantStore, DefineFormCommand, Message } from '@/lib/use-cases/assistant'
import DotLoader from '@/components/loaders/dot-loader'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ImperativePanelHandle } from 'react-resizable-panels'

const SHEET_CSS = "fixed inset-x-0 top-0 h-screen"
const CRITICAL_WIDTH = 600;

const CreateForm: NextPage = () => {
    const chatPanelRef = useRef<ImperativePanelHandle>(null)
    const [isCollapsed, setIsCollapsed] = useState(false);
    const [isMobile, setIsMobile] = useState(false);
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

        const checkWidth = () => {
            setIsMobile(window.innerWidth < CRITICAL_WIDTH)
            if (window.innerWidth < CRITICAL_WIDTH) {
                chatPanelRef.current?.collapse();
            }
        }

        checkWidth()
        window.addEventListener('resize', checkWidth)
        return () => window.removeEventListener('resize', checkWidth)
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

    const toggleCollapse = () => {
        const chatPanel = chatPanelRef.current;
        if (chatPanel?.isCollapsed()) {
            chatPanel.expand();
        } else {
            chatPanel?.collapse();
        }
    }

    const handleResize = (size: number) => {
        if (size > 300 && isCollapsed == false) {
            toggleCollapse();
            return
        }
    }

    return (
        <ResizablePanelGroup direction="horizontal" className={`${SHEET_CSS} flex flex-1 space-y-2`}>
            <ResizablePanel defaultSize={65}>
                <div className="flex h-screen sm:pl-14 lg-pl-16 sm:pt-12 md:pt-4">
                    {formModel && <PreviewFormContainer model={formModel} />}
                </div>
            </ResizablePanel>
            <ResizableHandle withHandle />
            <ResizablePanel
                ref={chatPanelRef}
                defaultSize={35}
                minSize={20}
                collapsible={true}
                collapsedSize={4}
                onCollapse={() => setIsCollapsed(true)}
                onExpand={() => setIsCollapsed(false)}
                onResize={(size) => handleResize(size)}
                className="transition-all duration-300 ease-in-out"
            >
                <div className="flex h-screen z-50 bg-background border-l pt-6 md:px-4">
                    <Button
                        variant="ghost"
                        size="icon"
                        className="absolute sm:pl-0 pl-4 opacity-50
                        `${isMobile ? 'hidden' : 'block'}`"
                        onClick={toggleCollapse}
                    >
                        {isCollapsed ? <ChevronLeft className="h-8 w-8 " /> : <ChevronRight className="h-8 w-8" />}
                    </Button>
                    {!isCollapsed && <div className="flex flex-col gap-4 sm:pt-12 p-6">
                        <ChatThread messages={messages} />
                        {isWaiting && <DotLoader className="flex flex-none items-center m-auto" />}
                        <ChatBox
                            className="flex-end flex-none"
                            placeholder="Ask folloup (⌘+F), ↑ to select"
                            onPendingChange={(pending) => { setIsWaiting(pending); }}
                            onStateChange={(stateCommand) => { defineFormHandler(stateCommand); }}
                        />
                    </div>}
                </div>
            </ResizablePanel>
        </ResizablePanelGroup>
    );
};

export default CreateForm;
