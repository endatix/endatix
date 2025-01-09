import React from "react";
import { Image, StyleSheet } from "@react-pdf/renderer";
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
  question,
  aspectRatio = "portrait",
}: FileViewerProps): React.ReactElement | null {
  const isImage = question.isFileImage(file);

  return isImage ? (
    <Image
      src={file.content}
      style={[
        styles.image,
        aspectRatio === "portrait" ? styles.portrait : styles.square,
      ]}
    />
  ) : null;
}

const styles = StyleSheet.create({
  image: {
    objectFit: "cover",
    width: "100%",
    height: "auto",
    wrap: "false" 
  },
  portrait: {
    width: 150,
    height: 200,
  },
  square: {
    width: 150,
    height: 150,
  },
});

export default PdfFileViewer;
