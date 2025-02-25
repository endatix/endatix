import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
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
  const envVars = Object.entries(process.env).filter(
    ([key]) =>
      !key.startsWith("npm_package") &&
      !key.startsWith("pnpm") &&
      !key.startsWith("ZSH") &&
      !key.startsWith("HOME") &&
      !key.startsWith("PWD") &&
      !key.startsWith("JAVA")
  );

  return (
    <Tabs defaultValue="all" className="flex items-center">
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
            {envVars.map(([key, value]) => (
              <li key={key}>
                <strong>{key}:</strong> {value}
              </li>
            ))}
          </ul>
        </div>
      </TabsContent>
    </Tabs>
  );
};

export default Dashboard;
