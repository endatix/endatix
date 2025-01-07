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
  width = 250,
  height = 330,
  question,
  aspectRatio = "portrait",
}: FileViewerProps): React.ReactElement | null {
  const isImage = question.isFileImage(file);

  return isImage ? (
    <Image
      src={file.content}
      style={[
        styles.image,
        { width: width, height: height },
        aspectRatio === "portrait" ? styles.portrait : styles.square,
      ]}
    />
  ) : null;
}

const styles = StyleSheet.create({
  image: {
    objectFit: "cover",
  },
  portrait: {
    aspectRatio: 3 / 4,
  },
  square: {
    aspectRatio: 1,
  },
});

export default PdfFileViewer;
