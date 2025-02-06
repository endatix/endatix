import React from "react";
import { StyleSheet, Image, Link, Text, View } from "@react-pdf/renderer";
import { File, FileType, getFileType } from "@/lib/questions/file/file-type";
import {
  DocumentFileIcon,
  UnknownFileIcon,
  VideoFileIcon,
} from "@/features/pdf-export/components/icons";
import { PDF_STYLES } from "@/features/pdf-export/components/pdf-styles";

interface FileViewerProps {
  file: File;
  width?: number;
  height?: number;
  aspectRatio?: "portrait" | "square";
}

export function PdfFileViewer({
  file,
  aspectRatio = "portrait",
}: FileViewerProps): React.ReactElement | null {
  const fileType = getFileType(file);

  switch (fileType) {
    case FileType.Image:
      return (
        <Image
          src={file.content}
          style={[
            styles.image,
            aspectRatio === "portrait" ? styles.portrait : styles.square,
          ]}
        />
      );
    case FileType.Video:
      return <FileDetails file={file} icon={<VideoFileIcon />} />;
    case FileType.Document:
      return <FileDetails file={file} icon={<DocumentFileIcon />} />;
    case FileType.Unknown:
    default:
      return <FileDetails file={file} icon={<UnknownFileIcon />} />;
  }
}

const FileDetails = ({ file, icon }: { file: File; icon: React.ReactNode }) => {
  return (
    <View
      style={[
        styles.portrait,
        PDF_STYLES.mutedBorder,
        PDF_STYLES.justifyBetween,
        PDF_STYLES.flexColumn,
        PDF_STYLES.marginBottom,
      ]}
    >
      <View style={[PDF_STYLES.flexColumn]}>
        <Text>{file?.name ?? "unknown file name"}</Text>
        <Text
          style={[
            PDF_STYLES.smallText,
            PDF_STYLES.mutedText,
            PDF_STYLES.marginBottom,
          ]}
        >
          {file.type ?? "unknown file type"}
        </Text>
      </View>
      <View style={[PDF_STYLES.flexRow]}>
        {icon}
        {file.content && <Link src={file.content}>Link to file</Link>}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  image: {
    objectFit: "cover",
    width: "100%",
    height: "auto",
    wrap: "false",
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
