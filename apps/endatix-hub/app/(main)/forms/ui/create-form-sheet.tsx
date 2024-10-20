"use client"

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { FC } from 'react';
import ChatBox from './chat-box';
import { Atom, BicepsFlexed, Code, Folder } from 'lucide-react';
import { cn } from '@/lib/utils';

interface FormCreateSheetProps {
  title: string;
  description: string;
  icon: React.ElementType;
  action: string;
  isSelected?: boolean;
}

const CreateFormCard: FC<FormCreateSheetProps> = ({ title, description, icon: Icon, action, isSelected }) => {
  return (
    <Card className={cn("hover:border-primary hover:bg-accent focus:outline focus:outline-2 focus:outline-primary-500", isSelected && "border-primary bg-accent")}>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-lg font-medium">{title}</CardTitle>
        <Icon className="h-8 w-8 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        <p className="text-xs text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
};

const CreateFormSheet: FC = () => {
  return (
    <SheetContent className="w-[600px] sm:w-[480px] sm:max-w-none flex flex-col h-screen justify-between">
      <SheetHeader className="mb-12">
        <SheetTitle>Create a Form</SheetTitle>
        <SheetDescription>
          Choose one of the following options to create a form.
        </SheetDescription>
      </SheetHeader>
      <div className="flex flex-wrap items-start justify-center flex-grow">
        <div className="grid grid-cols-2 gap-6">
          <CreateFormCard
            title="Use our AI Form Assistant"
            description="The recommended way to create a form."
            icon={Atom}
            action="Use AI Assistant"
            isSelected={true}
          />
          <CreateFormCard
            title="Create from a Template"
            description="Choose from a variety of templates to get started."
            icon={Folder}
            action="Create from Template"
          />
          <CreateFormCard
            title="Paste your JSON Code"
            description="You have your JSON code ready? Paste it here."
            icon={Code}
            action="Paste JSON Code"
          />
          <CreateFormCard
            title="Start from Scratch"
            description="Use the WYSIWYG Survey Creator to build your form."
            icon={BicepsFlexed}
            action="Start from Scratch"
          />
        </div>
      </div>
      <SheetFooter className="flex-end">
        <ChatBox/>
      </SheetFooter>
    </SheetContent>
  );
};

export default CreateFormSheet;
