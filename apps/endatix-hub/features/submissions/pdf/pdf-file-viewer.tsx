import React from "react";
import { Image, Text, View, StyleSheet } from "@react-pdf/renderer";
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file";

export interface File {
  content: string;
  name?: string;
  type?: string;
}

interface FileViewerProps {
  file: File;
  question: QuestionFileModelBase;
  width?: number;
  height?: number;
  aspectRatio?: "portrait" | "square";
}

export function PdfFileViewer({
  file,
  width = 250,
  height = 330,
  question,
  aspectRatio = "portrait",
}: FileViewerProps): React.ReactElement {
  const isImage = question.isFileImage(file);

  return (
    <View style={[styles.container, aspectRatio === "portrait" ? styles.portrait : styles.square]}>
      <View style={styles.mediaWrapper}>
        {isImage ? (
          <Image
            src={file.content}
            style={[
              styles.image,
              { width: width, height: height },
              aspectRatio === "portrait" ? styles.portrait : styles.square,
            ]}
          />
        ) : (
          <View style={[styles.placeholder, { width: width, height: height }]}>
            <Text>Document: {file.name}</Text>
          </View>
        )}
      </View>
      <View style={styles.details}>
        <Text style={styles.fileName}>{file.name}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginBottom: 8,
    flexDirection: "column",
    alignItems: "center",
  },
  mediaWrapper: {
    overflow: "hidden",
    borderRadius: 4,
    marginBottom: 4,
  },
  image: {
    objectFit: "cover",
    transition: "all 0.3s",
  },
  placeholder: {
    backgroundColor: "#e0e0e0",
    justifyContent: "center",
    alignItems: "center",
  },
  details: {
    marginTop: 4,
    alignItems: "center",
  },
  fileName: {
    fontSize: 12,
    fontWeight: "bold",
    textAlign: "center",
  },
  portrait: {
    aspectRatio: 3 / 4,
  },
  square: {
    aspectRatio: 1,
  },
});

export default PdfFileViewer;
