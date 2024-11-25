import PageTitle from '@/components/headings/page-title';
import { Separator } from '@/components/ui/separator';
import { getForm, getSubmissions } from '@/services/api';
import SubmissionsTable from './ui/submissions-table';

type Params = {
  params: Promise<{ formId: string }>
};

async function ResponsesPage({ params }: Params) {
  const { formId } = await params;
  const submissions = await getSubmissions(formId);
  const form = await getForm(formId);

  return (
    <>
      <PageTitle title={`Submissions for ${form.name}`} />
      <Separator className="my-4" />
      <SubmissionsTable data={submissions} />
    </>
  );
}

export default ResponsesPage;
