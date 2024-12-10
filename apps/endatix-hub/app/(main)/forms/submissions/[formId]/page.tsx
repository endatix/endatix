import PageTitle from '@/components/headings/page-title';
import { getForm, getSubmissions } from '@/services/api';
import SubmissionsTable from './ui/submissions-table';

type Params = {
  params: Promise<{ formId: string }>
};

async function ResponsesPage({ params }: Params) {
  const { formId } = await params;

  const [submissions, form] = await Promise.all([
    await getSubmissions(formId),
    await getForm(formId)
  ]);

  return (
    <>
      <PageTitle title={`Submissions for ${form.name}`} />
      <SubmissionsTable data={submissions} />
    </>
  );
}

export default ResponsesPage;
