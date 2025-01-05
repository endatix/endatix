import React from "react";
import { Text, View, StyleSheet } from "@react-pdf/renderer";
import { Question } from "survey-core";
import PdfFileAnswer from "./pdf-file-answer";
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file";

export interface ViewAnswerProps {
  forQuestion: Question;
}

export enum QuestionType {
  Text = "text",
  Boolean = "boolean",
  Rating = "rating",
  Radiogroup = "radiogroup",
  Dropdown = "dropdown",
  Ranking = "ranking",
  Matrix = "matrix",
  Comment = "comment",
  File = "file",
  Unsupported = "unsupported",
}

const PdfAnswerViewer = ({ forQuestion }: ViewAnswerProps): React.ReactElement => {
  const questionType = forQuestion.getType() ?? "unsupported";

  const renderTextAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderCheckboxAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>
        {forQuestion.value ? "Yes" : "No"}
      </Text>
    </View>
  );

  const renderRatingAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderRadiogroupAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderDropdownAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderRankingAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>
        {Array.isArray(forQuestion.value)
          ? forQuestion.value.join(", ")
          : "No Answer"}
      </Text>
    </View>
  );

  const renderMatrixAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>
        {JSON.stringify(forQuestion.value, null, 2) || "No Answer"}
      </Text>
    </View>
  );

  const renderCommentAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderFileAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
        <PdfFileAnswer
                question={forQuestion as QuestionFileModelBase}
            />
    </View>
  );

  const renderUnknownAnswer = () => (
    <View style={styles.answerContainer}>
      <Text style={styles.questionLabel}>{forQuestion.title}</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
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
  answerContainer: {
    marginBottom: 8,
  },
  questionLabel: {
    fontSize: 12,
    fontWeight: "bold",
    marginBottom: 4,
  },
  answerText: {
    fontSize: 12,
  },
});

export default PdfAnswerViewer;
