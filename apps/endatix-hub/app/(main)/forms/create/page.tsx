import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable';
import { NextPage } from 'next';
import ChatBox from '../ui/chat-box';
import PreviewFormContainer from './ui/preview-form-container';
import PageTitle from '@/components/headings/page-title';
import { Atom } from 'lucide-react';

const CreateForm: NextPage = () => {
    return (
        <div>
            <ResizablePanelGroup direction="horizontal" className="flex flex-1 space-y-2">
                <ResizablePanel defaultSize={68}>
                    <div className="flex h-screen p-8">
                        <PreviewFormContainer />
                    </div>
                </ResizablePanel>
                <ResizableHandle withHandle />
                <ResizablePanel defaultSize={32} minSize={20}>
                    <div className="flex flex-col h-screen z-50 gap-4 bg-background p-6 border-l">
                        <div className="flex items-center gap-2">
                            <PageTitle title="Create your form" />
                            <Atom className="h-12 w-12 text-muted-foreground" />
                        </div>
                        <ChatBox
                            className="flex-end flex-none"
                            placeholder="Ask folloup (⌘+F), ↑ to select"
                        />
                    </div>
                </ResizablePanel>
            </ResizablePanelGroup>
        </div>
    );
};

export default CreateForm;
