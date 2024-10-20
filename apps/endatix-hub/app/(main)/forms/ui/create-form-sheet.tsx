"use client"

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Atom, BicepsFlexed, Code, Folder} from 'lucide-react';
import { FC } from 'react';
import ChatBox from './chat-box';

interface FormCreateSheetProps {
}

const CreateFormSheet: FC<FormCreateSheetProps> = () => {
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
          <Card className="border-primary bg-accent focus:outline focus:outline-2 focus:outline-primary-500">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-lg font-medium">Use our AI Form Assistant</CardTitle>
              <Atom className="h-8 w-8 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <p className="text-xs text-muted-foreground">The recommended way to create a form.</p>
            </CardContent>
          </Card>
          <Card className="hover:border-primary hover:bg-accent focus:outline focus:outline-2 focus:outline-primary-500">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-lg font-medium">Create from a Template</CardTitle>
              <Folder className="h-8 w-8 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <p className="text-xs text-muted-foreground">Choose from a variety of templates to get started.</p>
            </CardContent>
          </Card>
          <Card className="hover:border-primary hover:bg-accent focus:outline focus:outline-2 focus:outline-primary-500">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-lg font-medium">Paste your JSON Code</CardTitle>
              <Code className="h-8 w-8 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <p className="text-xs text-muted-foreground">You have your JSON code ready? Paste it here.</p>
            </CardContent>
          </Card>
          <Card className="hover:border-primary hover:bg-accent focus:outline focus:outline-2 focus:outline-primary-500">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-lg font-medium">Start from Scratch</CardTitle>
              <BicepsFlexed className="h-8 w-8 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <p className="text-xs text-muted-foreground">Use the WYSIWYG Survey Creator to build your form.</p>
            </CardContent>
          </Card>
        </div>
      </div>
      <SheetFooter className="flex-end">
        <ChatBox/>
      </SheetFooter>
    </SheetContent>
  );
};

export default CreateFormSheet;

