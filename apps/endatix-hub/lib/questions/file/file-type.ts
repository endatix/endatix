/**
 * Represents a file with its content and metadata
 */
export interface File {
  content: string; // Base64 or URL content of the file
  name?: string; // Optional filename
  type?: string; // Optional MIME type
}

/**
 * Enumeration of supported file types for rendering
 */
export enum FileType {
  Image = "image", // Image files (jpg, png, etc)
  Video = "video", // Video files (mp4, etc)
  Document = "document", // PDF documents
  Unknown = "unknown", // Unsupported file types
}

/**
 * Determines the FileType based on the file's MIME type
 * @param file - The file object to analyze
 * @returns The detected FileType enum value
 */
export function getFileType(file: File): FileType {
  // Return unknown if file or required properties are missing
  if (!file || !file.content || !file.type) {
    return FileType.Unknown;
  }

  const mimeType = file.type.toLowerCase();

  // Check for image MIME types (image/jpeg, image/png, etc)
  if (mimeType.indexOf("image/") === 0) {
    return FileType.Image;
  }

  // Check for video MIME types (video/mp4, etc)
  if (mimeType.indexOf("video/") === 0) {
    return FileType.Video;
  }

  // Check for PDF documents
  if (mimeType.indexOf("application/pdf") === 0) {
    return FileType.Document;
  }

  // Return unknown for unsupported types
  return FileType.Unknown;
}
