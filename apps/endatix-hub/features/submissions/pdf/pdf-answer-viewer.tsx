import React from 'react';
import { Text, View, StyleSheet, Path, Svg } from '@react-pdf/renderer';
import { Question } from 'survey-core';
import PdfFileAnswer from './pdf-file-answer';
import { QuestionFileModelBase } from 'survey-core/typings/packages/survey-core/src/question_file';

export interface ViewAnswerProps {
  forQuestion: Question;
  panelTitle: string;
  pageBreak: boolean;
}

export enum QuestionType {
  Text = 'text',
  Boolean = 'boolean',
  Rating = 'rating',
  Radiogroup = 'radiogroup',
  Dropdown = 'dropdown',
  Ranking = 'ranking',
  Matrix = 'matrix',
  Comment = 'comment',
  File = 'file',
  Unsupported = 'unsupported',
}

const PdfAnswerViewer = ({
  forQuestion,
  panelTitle,
  pageBreak,
}: ViewAnswerProps): React.ReactElement => {
  const questionType = forQuestion.getType() ?? 'unsupported';
  const questionTitle = panelTitle ? `(${panelTitle}) ${forQuestion.title}` : forQuestion.title;

  const renderTextAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <Text style={styles.answerText}>{forQuestion.value || 'No Answer'}</Text>
    </View>
  );

  const renderCheckboxAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      {forQuestion.value ? (
        <Text style={[styles.booleanAnswer, styles.booleanYes]}>YES</Text>
      ) : (
        <Text style={[styles.booleanAnswer, styles.booleanNo]}>NO</Text>
      )}
    </View>
  );

  const renderRatingAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <Text style={styles.answerText}>{forQuestion.value || 'No Answer'}</Text>
    </View>
  );

  const renderRadiogroupAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <Text style={styles.answerText}>{forQuestion.value || 'No Answer'}</Text>
    </View>
  );

  const renderDropdownAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <Text style={styles.answerText}>{forQuestion.value || 'No Answer'}</Text>
    </View>
  );

  const renderRankingAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <Text style={styles.answerText}>
        {Array.isArray(forQuestion.value)
          ? forQuestion.value.join(', ')
          : 'No Answer'}
      </Text>
    </View>
  );

  const renderMatrixAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <Text style={styles.answerText}>
        {JSON.stringify(forQuestion.value, null, 2) || 'No Answer'}
      </Text>
    </View>
  );

  const renderCommentAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <Text style={styles.answerText}>{forQuestion.value || 'No Answer'}</Text>
    </View>
  );

  const MessageSquareTextIcon = () => (
    <Svg style={styles.icon}>
      <Path
        d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"
        stroke="currentColor"
        strokeWidth={2}
        stroke-linecap="round"
        stroke-linejoin="round"
      />
      <Path
        d="M13 8H7"
        stroke="currentColor"
        strokeWidth={2}
        stroke-linecap="round"
        stroke-linejoin="round"
      />
      <Path
        d="M17 12H7"
        stroke="currentColor"
        strokeWidth={2}
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </Svg>
  );

  const renderFileAnswer = () => (
    <View style={styles.fileAnswerContainer} break={pageBreak} wrap={false}>
      <Text style={styles.questionLabel}>
        {questionTitle}:
      </Text>
      <PdfFileAnswer question={forQuestion as QuestionFileModelBase} />
      {forQuestion?.supportComment() &&
        forQuestion?.hasComment &&
        forQuestion?.comment && (
          <View style={styles.flexRow}>
            <MessageSquareTextIcon />
            <View style={styles.flexColumn}>
              <Text style={[styles.questionLabel, styles.smallText]}>
                Comment:
              </Text>
              <Text style={[styles.mutedText, styles.smallText]}>
                {forQuestion?.comment}
              </Text>
            </View>
          </View>
        )}
    </View>
  );

  const renderUnknownAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>
        {questionTitle}
      </Text>
      <Text style={styles.answerText}>{forQuestion.value || 'No Answer'}</Text>
    </View>
  );

  switch (questionType) {
    case QuestionType.Text:
      return renderTextAnswer();
    case QuestionType.Boolean:
      return renderCheckboxAnswer();
    case QuestionType.Rating:
      return renderRatingAnswer();
    case QuestionType.Radiogroup:
      return renderRadiogroupAnswer();
    case QuestionType.Dropdown:
      return renderDropdownAnswer();
    case QuestionType.Ranking:
      return renderRankingAnswer();
    case QuestionType.Matrix:
      return renderMatrixAnswer();
    case QuestionType.Comment:
      return renderCommentAnswer();
    case QuestionType.File:
      return renderFileAnswer();
    default:
      return renderUnknownAnswer();
  }
};

const styles = StyleSheet.create({
  fileAnswerContainer: {
    marginBottom: 8,
    padding: 8,
  },
  booleanAnswer: {
    fontFamily: 'Roboto-Bold',
    fontSize: 10,
    padding: 3,
    textAlign: 'center',
  },
  booleanYes: {
    color: '#006105',
  },
  booleanNo: {
    color: '#FF0000',
  },
  questionLabel: {
    fontFamily: 'Roboto-Bold',
    fontSize: 12,
    marginBottom: 4,
    width: '40%',
  },
  answerText: {
    fontFamily: 'Roboto',
    fontSize: 12,
  },
  nonFileAnswerContainer: {
    fontFamily: 'Roboto',
    display: 'flex',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
    borderBottomStyle: 'solid',
    padding: 8,
    marginBottom: 16,
  },
  flexRow: {
    display: 'flex',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    alignItems: 'flex-start',
  },
  flexColumn: {
    display: 'flex',
    flexDirection: 'column',
    flexWrap: 'wrap',
  },
  icon: {
    width: 24,
    height: 24,
  },
  smallText: {
    fontSize: 10,
  },
  mutedText: {
    color: 'gray',
  },
});

export default PdfAnswerViewer;
