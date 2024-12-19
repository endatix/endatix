import { File, ListFilter, PlusCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getForms } from "@/services/api";
import FormsTable from "./forms-table";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";

const Dashboard = async () => {
  const forms = await getForms();
  const NEXT_PUBLIC_MAX_IMAGE_SIZE = process.env.NEXT_PUBLIC_MAX_IMAGE_SIZE;
  const SLACK_CLIENT_ID = process.env.SLACK_CLIENT_ID;
  const SLACK_CLIENT_SECRET = process.env.SLACK_CLIENT_SECRET;
  const NEXT_PUBLIC_SLK = process.env.NEXT_PUBLIC_SLK;
  const AZURE_STORAGE_CONTAINER_NAME = process.env.AZURE_STORAGE_CONTAINER_NAME;
  const AZURE_STORAGE_CONNECTION_STRING = process.env.AZURE_STORAGE_CONNECTION_STRING;
  const RESIZE_IMAGES = process.env.RESIZE_IMAGES;
  const RESIZE_IMAGES_WIDTH = process.env.RESIZE_IMAGES_WIDTH;
  const NEXT_PUBLIC_NAME = process.env.NEXT_PUBLIC_NAME;
  return (
    <Tabs defaultValue="all">
      <div className="flex items-center">
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="active">Active</TabsTrigger>
          <TabsTrigger value="draft">Draft</TabsTrigger>
          <TabsTrigger value="expired" className="hidden sm:flex">
            Expired
          </TabsTrigger>
          <TabsTrigger value="archived" className="hidden sm:flex">
            Archived
          </TabsTrigger>
        </TabsList>
        <div className="ml-auto flex items-center gap-2">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="h-8 gap-1">
                <ListFilter className="h-3.5 w-3.5" />
                <span className="sr-only sm:not-sr-only sm:whitespace-nowrap">
                  Filter
                </span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>Filter by</DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuCheckboxItem checked>
                Active
              </DropdownMenuCheckboxItem>
              <DropdownMenuCheckboxItem>Draft</DropdownMenuCheckboxItem>
              <DropdownMenuCheckboxItem>Expired</DropdownMenuCheckboxItem>
              <DropdownMenuCheckboxItem>Archived</DropdownMenuCheckboxItem>
            </DropdownMenuContent>
          </DropdownMenu>
          <Button size="sm" variant="outline" className="h-8 gap-1">
            <File className="h-3.5 w-3.5" />
            <span className="sr-only sm:not-sr-only sm:whitespace-nowrap">
              Export
            </span>
          </Button>
          <Button size="sm" className="h-8 gap-1">
            <PlusCircle className="h-3.5 w-3.5" />
            <span className="sr-only sm:not-sr-only sm:whitespace-nowrap">
              Add Form
            </span>
          </Button>
        </div>
      </div>
      <TabsContent value="all">
        <Card x-chunk="dashboard-06-chunk-0">
          <CardHeader>
            <CardTitle>Forms</CardTitle>
            <CardDescription>
              Manage your forms and view form submissions.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <FormsTable data={forms}></FormsTable>
          </CardContent>
          <CardFooter>
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <div className="text-xs text-muted-foreground">
                    Showing <strong>1-{forms.length}</strong> of{" "}
                    <strong>{forms.length}</strong> forms
                  </div>
                </TooltipTrigger>
                <TooltipContent>TODO: Add support for paging</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </CardFooter>
        </Card>
      </TabsContent>
      <TabsContent value="draft">
        <div>
          <ul>
            <li>NEXT_PUBLIC_MAX_IMAGE_SIZE: {NEXT_PUBLIC_MAX_IMAGE_SIZE}</li>
            <li>SLACK_CLIENT_ID: {SLACK_CLIENT_ID}</li>
            <li>NEXT_PUBLIC_NAME: {NEXT_PUBLIC_NAME}</li>
            <li>SLACK_CLIENT_SECRET_length: {SLACK_CLIENT_SECRET?.length?? 0}</li>
            <li>NEXT_PUBLIC_SLK: {NEXT_PUBLIC_SLK}</li>
            <li>AZURE_STORAGE_CONTAINER_NAME_length: {AZURE_STORAGE_CONTAINER_NAME}</li>
            <li>AZURE_STORAGE_CONNECTION_STRING: {AZURE_STORAGE_CONNECTION_STRING?.length?? 0}</li>
            <li>RESIZE_IMAGES: {RESIZE_IMAGES}</li>
            <li>RESIZE_IMAGES_WIDTH: {RESIZE_IMAGES_WIDTH}</li>
          </ul>
        </div>
      </TabsContent>
    </Tabs>
  );
};

export default Dashboard;
