import React from 'react';
import {
  Page,
  Text,
  View,
  Document,
  StyleSheet,
  Font,
} from '@react-pdf/renderer';
import { Model, PanelModel, Question } from 'survey-core';
import PdfAnswerViewer from '@/features/submissions/pdf/pdf-answer-viewer';
import { Submission } from '@/types';
import { getElapsedTimeString, parseDate } from '@/lib/utils';
import { registerSpecializedQuestion, SpecializedVideo } from '@/lib/questions';

Font.register({
  family: 'Roboto',
  fonts: [{ src: './public/assets/fonts/Roboto-Regular.ttf' }],
});

Font.register({
  family: 'Roboto-Bold',
  fonts: [{ src: './public/assets/fonts/Roboto-Bold.ttf' }],
});

type SubmissionDataPdfProps = {
  submission: Submission;
};

// TODO: This is a duplicate of function in submission-properties.tsx
const getFormattedDate = (date: Date): string => {
  const parsedDate = parseDate(date);
  if (!parsedDate) {
    return '-';
  }

  return parsedDate.toLocaleString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    month: 'short',
    day: '2-digit',
    year: 'numeric',
    hour12: true,
  });
};

registerSpecializedQuestion(SpecializedVideo);

export const SubmissionDataPdf = ({ submission }: SubmissionDataPdfProps) => {
  if (!submission.formDefinition) {
    return <Text>Form definition not found</Text>;
  }
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

  // TODO: This is a duplicate of a function in question-label.tsx
  const getPanelTitle = (question: Question) => {
    const panel = question.parent;
    if (panel instanceof PanelModel) {
      return panel.title;
    }
    return '';
  };

  let lastPanel: string | null = null;

  return (
    <Document>
      <Page style={styles.page}>
        <View style={[styles.section, styles.sectionProperties]}>
          <Text style={styles.sectionTitle}>Submission Properties</Text>
          <Text>ID: {submission.id}</Text>
          <Text>Completed: {submission.isComplete ? 'Yes' : 'No'}</Text>
          <Text>Created on: {getFormattedDate(submission.createdAt)}</Text>
          <Text>Comleted on: {getFormattedDate(submission.completedAt)}</Text>
          <Text>
            Completion time:{' '}
            {getElapsedTimeString(
              submission.createdAt,
              submission.completedAt,
              'long'
            )}
          </Text>
          <Text>
            Last modified on: {getFormattedDate(submission.modifiedAt)}
          </Text>
        </View>
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Submission Answers</Text>
          <View style={styles.questions}>
            {questions?.map((question) => {
              const panelTitle = getPanelTitle(question);
              let pageBreak: boolean = false;

              if (!lastPanel) {
                lastPanel = panelTitle;
              }

              if (lastPanel != panelTitle) {
                pageBreak = true;
                lastPanel = panelTitle;
              } else {
                pageBreak = false;
              }

              return (
                <PdfAnswerViewer
                  key={question.id}
                  forQuestion={question}
                  panelTitle={panelTitle}
                  pageBreak={pageBreak}
                />
              );
            })}
          </View>
        </View>
      </Page>
    </Document>
  );
};

const styles = StyleSheet.create({
  page: {
    padding: 20,
    fontSize: 12,
    fontFamily: 'Roboto',
  },
  section: {
    marginBottom: 16,
    padding: 8,
  },
  sectionProperties: {
    fontSize: 8,
    backgroundColor: '#f0f0f0',
    borderRadius: 4,
    gap: 4,
  },
  sectionTitle: {
    fontSize: 14,
    marginBottom: 8,
    fontFamily: 'Roboto-Bold',
  },
  questions: {
    marginTop: 8,
  },
  question: {
    flexDirection: 'row',
    marginBottom: 4,
  },
});
