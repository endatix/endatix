import dynamic from 'next/dynamic';
const SurveyComponent = dynamic(() => import('@/components/survey'), {
  ssr: false, 
});


export default async function Survey({ params }: { params: { formId: string } }) {
  // console.log(params.formId);
  const surveyJson = await getServerSideProps(params.formId);
  return (
    <div className="flex min-h-screen flex-col items-center p-8">
      <SurveyComponent definition={surveyJson} formId={params.formId} />
    </div>
  );
}

const getServerSideProps = async (formId: string) => {

  const res = await fetch('https://localhost:5001/api/forms/'+ formId +'/definition');
  const surveyJson = await res.json();

  return surveyJson.jsonData;
}