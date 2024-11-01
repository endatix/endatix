'use client'

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable'
import { NextPage } from 'next'
import ChatBox from '../ui/chat-box'
import PreviewFormContainer from './ui/preview-form-container'
import ChatThread from './ui/chat-thread'
import { useEffect, useRef, useState, useTransition } from 'react'
import { AssistantStore, CreateFormRequest, DefineFormCommand, Message } from '@/lib/use-cases/assistant'
import DotLoader from '@/components/loaders/dot-loader'
import { ChevronLeft, ChevronRight, FilePenLine, Globe, PlusCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ImperativePanelHandle } from 'react-resizable-panels'
import { redirect } from 'next/navigation'
import { createFormDraft } from './create-form.action'

const SHEET_CSS = "absolute inset-x-0 top-0 h-screen"
const CRITICAL_WIDTH = 600;

const CreateForm: NextPage = () => {
    const chatPanelRef = useRef<ImperativePanelHandle>(null)
    const [isCollapsed, setIsCollapsed] = useState(false);
    const [isMobile, setIsMobile] = useState(false);
    const [shouldType, setShouldType] = useState(false);
    const [isWaiting, setIsWaiting] = useState(false);
    const [messages, setMessages] = useState(new Array<Message>());
    const [formModel, setFormModel] = useState<any>({});
    const [isPending, startTransition] = useTransition()

    useEffect(() => {
        const contextStore = new AssistantStore();
        const currentContext = contextStore.getChatContext();

        if (currentContext?.isInitialPrompt) {
            setShouldType(true);
        }

        if (currentContext?.messages) {
            setMessages(currentContext.messages);
        }
        const formModel = contextStore.getFormModel();
        (formModel);
        setFormModel(formModel);

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
                setShouldType(true);
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

    const openFormInEditor = async () => {
        startTransition(async () => {

            const request: CreateFormRequest = {
                name: formModel["title"],
                isEnabled: false,
                description: formModel["description"],
                formDefinitionJsonData: JSON.stringify(formModel)
            }
            const formResult = await createFormDraft(request);
            if (formResult.isSuccess && formResult.formId) {
                redirect(`/forms/${formResult.formId}`);
            } else {
                alert(formResult.error);
            }
        });
    }

    return (
        <ResizablePanelGroup direction="horizontal" className={`${SHEET_CSS} flex flex-1 space-y-2`}>
            <ResizablePanel defaultSize={61}>
                <div className="flex h-screen sm:pl-14 lg-pl-16 sm:pt-12 md:pt-4">
                    {formModel && <PreviewFormContainer model={formModel} />}
                </div>
            </ResizablePanel>
            <ResizableHandle />
            <ResizablePanel
                ref={chatPanelRef}
                defaultSize={39}
                minSize={20}
                collapsible={true}
                collapsedSize={4}
                onCollapse={() => setIsCollapsed(true)}
                onExpand={() => setIsCollapsed(false)}
                onResize={(size) => handleResize(size)}
                className="transition-all duration-300 ease-in-out"
            >
                <div className="flex h-screen shrink-0 z-50 bg-background border-l pt-6 md:px-4">
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
                        <ChatThread isTyping={shouldType} messages={messages} />
                        {isWaiting && <DotLoader className="flex flex-none items-center m-auto" />}
                        <div className="items-center gap-2 flex">
                            <Button variant="outline" size="sm" className="h-8 border-dashed">
                                <Globe className="mr-2 h-4 w-4" />
                                Add languages
                            </Button>
                            <Button variant="outline" size="sm" className="h-8 border-dashed">
                                <PlusCircle className="mr-2 h-4 w-4" />
                                Generate submissions
                            </Button>
                            <Button
                                disabled={isPending}
                                onClick={openFormInEditor}
                                variant="default"
                                size="sm"
                                className="h-8 border-dashed"
                            >
                                <FilePenLine className="mr-2 h-4 w-4" />
                                {isPending ? 'Creating Form...' : 'Continue in Editor'}
                            </Button>
                        </div>
                        <ChatBox
                            className="flex-end flex-none"
                            placeholder="Ask a follow up (⌘F), ↑ to select"
                            onPendingChange={(pending) => { setIsWaiting(pending); }}
                            onStateChange={(stateCommand) => { defineFormHandler(stateCommand); }}
                        />
                    </div>}
                </div>
            </ResizablePanel >
        </ResizablePanelGroup >
    );
};

export default CreateForm;
