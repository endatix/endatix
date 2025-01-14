import { useMemo, useEffect } from "react";
import { Model } from "survey-core";
import { Submission } from "@/types";

export function useSurveyModel(definition: string, submission?: Submission) {
  // Create survey model only when definition changes
  const surveyModel = useMemo(() => {
    return new Model(definition);
  }, [definition]);

  // Handle submission updates via effect
  useEffect(() => {
    if (submission) {
      try {
        surveyModel.data = JSON.parse(submission.jsonData);
        surveyModel.currentPageNo = submission.currentPage ?? 0;
      } catch (error) {
        console.debug("Failed to parse submission data", error);
      }
    }
  }, [submission, surveyModel]);

  return surveyModel;
}
