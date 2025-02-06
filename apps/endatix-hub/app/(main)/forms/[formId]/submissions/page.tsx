import PageTitle from "@/components/headings/page-title";
import { getForm, getSubmissions } from "@/services/api";
import SubmissionsTable from "./ui/submissions-table";
import type { Metadata, ResolvingMetadata } from "next";
import { Suspense } from "react";
import { Skeleton } from "@/components/ui/skeleton";

type Params = {
  params: Promise<{ formId: string }>;
  searchParams: Promise<{
    page: string;
    pageSize: string;
    useLegacyTable: boolean;
  }>;
};

export async function generateMetadata(
  { params, searchParams }: Params,
  parent: ResolvingMetadata,
): Promise<Metadata> {
  const { formId } = await params;
  const form = await getForm(formId);

  return {
    title: `Submissions for ${form.name}`,
    description: `View all submissions for ${form.name}`,
    openGraph: {
      title: `Search params: ${JSON.stringify(await searchParams)}`,
      description: `Parent title: ${(await parent).title}`,
    },
  };
}

export default async function ResponsesPage({ params, searchParams }: Params) {
  const { formId } = await params;
  const { useLegacyTable, pageSize } = await searchParams;

  return (
    <>
      <Suspense fallback={<PageTitle title="Submissions..." />}>
        <PageTitleData formId={formId} />
      </Suspense>
      <Suspense fallback={<TableLoader pageSize={pageSize} />}>
        <SubmissionsTableData
          formId={formId}
          useLegacyTable={useLegacyTable ?? false}
        />
      </Suspense>
    </>
  );
}

async function PageTitleData({ formId }: { formId: string }) {
  const form = await getForm(formId);
  return <PageTitle title={`Submissions for ${form.name}`} />;
}

async function SubmissionsTableData({
  formId,
  useLegacyTable,
}: {
  formId: string;
  useLegacyTable: boolean;
}) {
  const submissions = await getSubmissions(formId);
  return (
    <SubmissionsTable
      data={submissions}
      useLegacyTable={useLegacyTable ?? false}
    />
  );
}

function TableLoader({ pageSize }: { pageSize: string }) {
  const pageSizeNumber = parseInt(pageSize) || 10;
  const rowHeight = 60;
  const rows = Array.from({ length: pageSizeNumber }, (_, i) => i + 1);
  return (
    <div className="flex flex-col space-y-3 relative w-full overflow-auto">
      <Skeleton className={`h-[${rowHeight}px] bg-gray-200 w-full p-4`} />
      {rows.map((row) => (
        <Skeleton key={row} className={`h-[${rowHeight}px] w-full p-4`} />
      ))}
      <Skeleton className={`h-[${rowHeight}px] bg-gray-200 w-full p-4`} />
    </div>
  );
}
