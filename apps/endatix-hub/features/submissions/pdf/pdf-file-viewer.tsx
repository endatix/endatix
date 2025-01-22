import React from 'react';
import {
  StyleSheet,
  Image,
  Link,
  Path,
  Svg,
  Text,
  View,
} from '@react-pdf/renderer';

export interface File {
  content: string;
  name?: string;
  type?: string;
}

interface FileViewerProps {
  file: File;
  width?: number;
  height?: number;
  aspectRatio?: 'portrait' | 'square';
}

export function PdfFileViewer({
  file,
  aspectRatio = 'portrait',
}: FileViewerProps): React.ReactElement | null {
  const isImage = file.type?.includes('image');
  return isImage ? (
    <Image
      src={file.content}
      style={[
        styles.image,
        aspectRatio === 'portrait' ? styles.portrait : styles.square,
      ]}
    />
  ) : (
    <View style={[styles.portrait, styles.grayBackground]}>
      <Text>{file?.name}</Text>
      <Text style={[styles.smallText, styles.mutedText, styles.marginBottom]}>
        {file.type}
      </Text>
      <View style={[styles.flexRow, styles.marginBottom]}>
        <VideoFileIcon />
        <Link src={file.content}>Link to file</Link>
      </View>
    </View>
  );
}

const VideoFileIcon = () => (
  <Svg style={styles.icon}>
    <Path
      d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    />
    <Path
      d="M14 2v4a2 2 0 0 0 2 2h4"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    />
    <Path
      d="m10 11 5 3-5 3v-6Z"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </Svg>
);

const styles = StyleSheet.create({
  image: {
    objectFit: 'cover',
    width: '100%',
    height: 'auto',
    wrap: 'false',
  },
  portrait: {
    width: 150,
    height: 200,
  },
  square: {
    width: 150,
    height: 150,
  },
  icon: {
    width: 24,
    height: 24,
  },
  flexRow: {
    display: 'flex',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    alignItems: 'center',
  },
  flexColumn: {
    display: 'flex',
    flexDirection: 'column',
    flexWrap: 'wrap',
  },
  smallText: {
    fontSize: 10,
  },
  mutedText: {
    color: 'gray',
  },
  border: {
    borderWidth: 1,
    borderColor: 'gray',
    borderStyle: 'solid',
    padding: 8,
    marginBottom: 16,
  },
  grayBackground: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 4,
    padding: 8,
  },
  marginBottom: {
    marginBottom: 8,
  },
});

export default PdfFileViewer;
