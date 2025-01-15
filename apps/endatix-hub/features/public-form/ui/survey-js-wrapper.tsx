"use client";

import { Submission } from "@/types";
import dynamic from "next/dynamic";

const SurveyComponent = dynamic(() => import("./survey-component"), {
  ssr: false,
});

interface SurveyJsWrapperProps {
  definition: string;
  formId: string;
  submission?: Submission | undefined;
}

const SurveyJsWrapper = ({
  formId,
  definition,
  submission,
}: SurveyJsWrapperProps) => {
  return (
    <SurveyComponent
      formId={formId}
      definition={definition}
      submission={submission}
    />
  );
};

export default SurveyJsWrapper;
