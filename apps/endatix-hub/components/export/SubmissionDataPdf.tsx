import React from "react";
import { Page, Text, View, Document, StyleSheet } from "@react-pdf/renderer";
import { Model } from "survey-core";
import PdfAnswerViewer from "@/features/submissions/pdf/pdf-answer-viewer";

type SubmissionDataPdfProps = {
  submission: any;
};

export const SubmissionDataPdf = ({ submission }: SubmissionDataPdfProps) => {
  const json = JSON.parse(submission.formDefinition.jsonData);
  const surveyModel = new Model(json);

  let submissionData = {};
  try {
    submissionData = JSON.parse(submission?.jsonData);
  } catch (ex) {
    console.warn("Error while parsing submission's JSON data", ex);
  }

  surveyModel.data = submissionData;
  const questions = surveyModel.getAllQuestions(false, false, true);

  return (
    <Document>
      <Page style={styles.page}>
        <Text style={styles.sectionTitle}>Submission Properties</Text>
        <View style={styles.section}>
          <Text>ID: {submission.id}</Text>
          <Text>Submitted On: {submission.submittedOn}</Text>
        </View>

        <Text style={styles.sectionTitle}>Submission Answers</Text>
        <View style={styles.questions}>
        {questions?.map((question) => {
                    return (
                        <div key={question.id}
                            className="grid grid-cols-5 items-center gap-4">
                            <PdfAnswerViewer key={question.id} forQuestion={question} />
                        </div>
                    );
                })}
        </View>
      </Page>
    </Document>
  );
};

const styles = StyleSheet.create({
  page: {
    padding: 20,
    fontSize: 12,
  },
  sectionTitle: {
    fontSize: 14,
    marginBottom: 8,
    fontWeight: "bold",
  },
  section: {
    marginBottom: 16,
  },
  questions: {
    marginTop: 8,
  },
  question: {
    flexDirection: "row",
    marginBottom: 4,
  },
});
