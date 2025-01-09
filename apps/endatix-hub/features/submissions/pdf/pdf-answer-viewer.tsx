import React, { useMemo } from "react";
import { Text, View, StyleSheet, Font } from "@react-pdf/renderer";
import { PanelModel, Question } from "survey-core";
import PdfFileAnswer from "./pdf-file-answer";
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file";

export interface ViewAnswerProps {
  forQuestion: Question;
  panelTitle: string;
  pageBreak: boolean;
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

const PdfAnswerViewer = ({ forQuestion, panelTitle, pageBreak }: ViewAnswerProps): React.ReactElement => {
  const questionType = forQuestion.getType() ?? "unsupported";
   

  const renderTextAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderCheckboxAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      {forQuestion.value ? (
        <Text style={[styles.booleanAnswer, styles.booleanYes]}>YES</Text>
      ) : (
        <Text style={[styles.booleanAnswer, styles.booleanNo]}>NO</Text>
      )}
    </View>
  );

  const renderRatingAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderRadiogroupAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderDropdownAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderRankingAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      <Text style={styles.answerText}>
        {Array.isArray(forQuestion.value)
          ? forQuestion.value.join(", ")
          : "No Answer"}
      </Text>
    </View>
  );

  const renderMatrixAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      <Text style={styles.answerText}>
        {JSON.stringify(forQuestion.value, null, 2) || "No Answer"}
      </Text>
    </View>
  );

  const renderCommentAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
      <Text style={styles.answerText}>{forQuestion.value || "No Answer"}</Text>
    </View>
  );

  const renderFileAnswer = () => (
    <View style={styles.fileAnswerContainer} break={pageBreak} wrap={false}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
        <PdfFileAnswer
                question={forQuestion as QuestionFileModelBase}
            />
    </View>
  );

  const renderUnknownAnswer = () => (
    <View style={styles.nonFileAnswerContainer} break={pageBreak}>
      <Text style={styles.questionLabel}>({panelTitle}) {forQuestion.title}:</Text>
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
  fileAnswerContainer: {
    marginBottom: 8,
    padding: 8,
  },
  booleanAnswer: {
    fontFamily: 'Roboto-Bold',
    fontSize: 10,
    padding: 3,
    textAlign: "center",
  },
  booleanYes: {
    color: "#006105",
  },
  booleanNo: {
    color: "#FF0000" ,
  },
  questionLabel: {
    fontFamily: 'Roboto-Bold',
    fontSize: 12,
    marginBottom: 4,
    width: "40%"
  },
  answerText: {
    fontFamily: 'Roboto',
    fontSize: 12,
  },
  nonFileAnswerContainer: {
    fontFamily: 'Roboto',
    display: "flex", 
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 10, 
    borderBottomWidth: 1, 
    borderBottomColor: '#f0f0f0', 
    borderBottomStyle: 'solid', 
    padding: 8,
    marginBottom: 16
  },
});

export default PdfAnswerViewer;
