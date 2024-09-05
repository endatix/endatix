import { getSubmissionsByFormId } from '@/services/api';
import { Submission } from '@/types';

type ResponsesProps = {
  responses: Response[];
};

const Responses = async ({ params }: { params: { formId: string } }) => {
  const { formId } = params;
  const responses = await getSubmissionsByFormId(formId);

  return (
    <div>
      <h1>Responses for {formId}</h1>
      {responses.length > 0 ? (
        <ul>
          {responses.map((response) => (
            <li key={response.id}>{response.jsonData}</li>
          ))}
        </ul>
      ) : (
        <p>No responses found.</p>
      )}
    </div>
  );
};

export default Responses;
