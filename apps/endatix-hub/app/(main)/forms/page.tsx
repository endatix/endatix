import { getForms } from "@/services/api";
import FormsList from "./ui/forms-list";
import PageTitle from "@/components/headings/page-title";
import { Button } from "@/components/ui/button";
import { FilePlus2 } from "lucide-react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Sheet, SheetTrigger } from "@/components/ui/sheet";
import CreateFormSheet from "./ui/create-form-sheet";

const Forms = async () => {
  const forms = await getForms();

  return (
    <>
      <PageTitle title="Forms" />
      <div className="flex-1 space-y-2">
        <Tabs defaultValue="all" className="space-y-0">
          <div className="flex items-center justify-between space-y-0 mb-4">
            <TabsList>
              <TabsTrigger value="all">All</TabsTrigger>
              <TabsTrigger value="public" disabled>Public </TabsTrigger>
              <TabsTrigger value="private" disabled>Private</TabsTrigger>
            </TabsList>
            <div className="flex items-center space-x-2">
              <Sheet modal={false}>
                <SheetTrigger asChild>
                  <Button variant="default">
                    <FilePlus2 className="mr-2 h-4 w-4" />
                    Create a Form
                  </Button>
                </SheetTrigger>
                <CreateFormSheet />
              </Sheet>
            </div>
          </div>
          <TabsContent value="all">
            <FormsList forms={forms} />
          </TabsContent>
        </Tabs>
      </div>
    </>
  );
};

export default Forms;
