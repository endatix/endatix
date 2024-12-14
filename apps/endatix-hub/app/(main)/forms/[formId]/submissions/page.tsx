import PageTitle from '@/components/headings/page-title';
import { getForm, getSubmissions } from '@/services/api';
import SubmissionsTable from './ui/submissions-table';
import type { Metadata, ResolvingMetadata } from 'next'

type Params = {
  params: Promise<{ formId: string }>
  searchParams: Promise<{ 
    page: string, 
    pageSize: string,
    useLegacyTable: boolean
   }>
}

export async function generateMetadata(
  { params, searchParams }: Params,
  parent: ResolvingMetadata
): Promise<Metadata> {
  const { formId } = await params;
  const form = await getForm(formId);

  return {
    title: `Submissions for ${form.name}`,
    description: `View all submissions for ${form.name}`,
    openGraph: {
      title: `Search params: ${JSON.stringify((await searchParams))}`,
      description: `Parent title: ${(await parent).title}`,
    },
  };
}

const logSearchParams = async (searchParams: Promise<{ page: string, pageSize: string }>) => {
  const { page, pageSize } = await searchParams;
  if (page) {
    console.log("page", page);
  }
  if (pageSize) {
    console.log("pageSize", pageSize);
  }
}

async function ResponsesPage({ params, searchParams }: Params) {
  const { formId } = await params;
  const [submissions, form] = await Promise.all([
    await getSubmissions(formId),
    await getForm(formId)
  ]);
  await logSearchParams(searchParams);
  const { useLegacyTable } = await searchParams;

  return (
    <>
      <PageTitle title={`Submissions for ${form.name}`} />
      <SubmissionsTable
        data={submissions}
        useLegacyTable={useLegacyTable?? false}
      />
    </>
  );
}

export default ResponsesPage;
