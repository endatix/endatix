import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable';
import { NextPage } from 'next';
import ChatBox from '../ui/chat-box';
import PreviewFormContainer from './ui/preview-form-container';

const CreateForm: NextPage = () => {
    return (
        <div className="flex-1 space-y-2 h-full">
            <ResizablePanelGroup direction="horizontal">
                <ResizablePanel className="flex min-h-screen flex-col p-8">
                    <PreviewFormContainer />
                </ResizablePanel>
                <ResizableHandle withHandle />
                <ResizablePanel className="h-full z-50 gap-4 bg-background p-6 inset-y-0 right-0 border-l min-w-[300px]">
                    <ChatBox />
                </ResizablePanel>
            </ResizablePanelGroup>
        </div>
    );
};

export default CreateForm;
