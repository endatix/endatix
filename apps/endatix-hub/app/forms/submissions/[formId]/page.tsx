import PageTitle from '@/components/headings/page-title';
import { Separator } from '@/components/ui/separator';
import { getFormById, getSubmissionsByFormId } from '@/services/api';
import SubmissionsTable from './ui/submissions-table';

const Responses = async ({ params }: { params: { formId: string } }) => {
  const { formId } = params;
  const submissions = await getSubmissionsByFormId(formId);
  const form = await getFormById(formId);

  return (
    <>
      <PageTitle title={`Submissions for ${form.name}`} />
      <Separator className="my-4" />
      <SubmissionsTable data={submissions} />
    </>
  );
}

export default Responses;
