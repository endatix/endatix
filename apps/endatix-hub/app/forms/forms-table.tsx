import Image from "next/image";
import { MoreHorizontal } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Form } from "@/types";
import Link from "next/link";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";

type FormDataProps = {
  data: Form[];
};

const FormsTable = ({ data }: FormDataProps) => (
  <Table>
    <TableHeader>
      <TableRow>
        <TableHead className="hidden w-[100px] sm:table-cell">
          <span className="sr-only">Image</span>
        </TableHead>
        <TableHead>Name</TableHead>
        <TableHead>Status</TableHead>
        <TableHead>Is Enabled</TableHead>
        <TableHead className="hidden md:table-cell">Submission Count</TableHead>
        <TableHead className="hidden md:table-cell">Created at</TableHead>
        <TableHead>
          <span className="sr-only">Actions</span>
        </TableHead>
      </TableRow>
    </TableHeader>
    <TableBody>
      {data.map((form) => (
        <TableRow key={form.name}>
          <TableCell className="hidden sm:table-cell">
            <Image
              alt="Form image"
              className="aspect-square rounded-md object-cover"
              height="64"
              src="/placeholder.svg"
              width="64"
            />
          </TableCell>
          <TableCell className="font-medium">{form.name}</TableCell>
          <TableCell>
            <Badge variant="outline">Draft</Badge>
          </TableCell>
          <TableCell className="hidden md:table-cell">
            <Badge variant="secondary">{form.isEnabled ? "Yes" : "No"}</Badge>
          </TableCell>
          <TableCell className="hidden md:table-cell">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Link href={`/submissions/${form.id}`}>
                    {Math.round(Math.random() * 25)}
                  </Link>
                </TooltipTrigger>
                <TooltipContent>View submissions</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </TableCell>
          <TableCell className="hidden md:table-cell">
            {new Date(form.dateTimeCreated).toLocaleString("en-US")}
          </TableCell>
          <TableCell>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button aria-haspopup="true" size="icon" variant="ghost">
                  <MoreHorizontal className="h-4 w-4" />
                  <span className="sr-only">Toggle menu</span>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>Actions</DropdownMenuLabel>
                <DropdownMenuItem>Edit</DropdownMenuItem>
                <DropdownMenuItem>Delete</DropdownMenuItem>
                <DropdownMenuItem>
                  <Link href={`/submissions/${form.id}`}>View Submissions</Link>
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </TableCell>
        </TableRow>
      ))}
    </TableBody>
  </Table>
);

export default FormsTable;
