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
    <View style={styles.container} wrap={false}>
      {files && files.length > 0 ? (
        <View style={styles.filesContainer} wrap={false}>
          {files.map((file, index) => (
            <PdfFileViewer
              key={index}
              file={file}
              question={question}
              width={150}
              height={200}
              aspectRatio="portrait"
            />
          ))}
        </View>
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
    break: true
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
  filesContainer: {
    display: "flex", 
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 10, 
    wrap: false,
    break: "avoid"
  },
});

export default PdfFileAnswer;
