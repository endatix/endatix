import React from "react";
import { View, Text, StyleSheet } from "@react-pdf/renderer";
import { PdfFileViewer, File } from "./pdf-file-viewer";
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file";

interface FileAnswerProps {
  question: QuestionFileModelBase;
}

export function PdfFileAnswer({ question }: FileAnswerProps): React.ReactElement {
  const files = question?.value as File[];

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Uploaded Files:</Text>
      {files && files.length > 0 ? (
        files.map((file, index) => (
          <PdfFileViewer
            key={index}
            file={file}
            question={question}
            width={150}
            height={200}
            aspectRatio="portrait"
          />
        ))
      ) : (
        <Text style={styles.noFiles}>No files uploaded</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginBottom: 16,
    padding: 10,
  },
  title: {
    fontSize: 14,
    marginBottom: 8,
    fontWeight: "bold",
  },
  noFiles: {
    fontSize: 12,
    color: "gray",
  },
});

export default PdfFileAnswer;
