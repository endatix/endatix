import Image from 'next/image';
import { cn } from '@/lib/utils';

export interface File {
  content: string;
  name?: string;
  type?: string;
}

interface FileViewerProps extends React.HTMLAttributes<HTMLDivElement> {
  file: File;
  width?: number;
  height?: number;
  aspectRatio?: 'portrait' | 'square';
}

export enum FileType {
  Image = 'image',
  Video = 'video',
  Document = 'document',
  Unknown = 'unknown',
}

export function getFileType(file: File): FileType {
  if (!file || !file.content || !file.type) {
    return FileType.Unknown;
  }

  const mimeType = file.type.toLowerCase();

  if (mimeType.indexOf('image/') === 0) {
    return FileType.Image;
  }

  if (mimeType.indexOf('video/') === 0) {
    return FileType.Video;
  }

  if (mimeType.indexOf('application/pdf') === 0) {
    return FileType.Document;
  }

  return FileType.Unknown;
}

export function FileViewer({
  file,
  width,
  height,
  className,
  aspectRatio = 'portrait',
  ...props
}: FileViewerProps) {
  const fileType = getFileType(file);

  return (
    <div className={cn('space-y-3', className)} {...props}>
      <div className="overflow-hidden rounded-md">
        {fileType === FileType.Image && (
          <Image
            src={file.content}
            alt={file.name || ''}
            width={width}
            height={height}
            className={cn(
              'h-auto w-auto object-cover transition-all hover:scale-105',
              aspectRatio === 'portrait' ? 'aspect-[3/4]' : 'aspect-square'
            )}
          />
        )}
        {fileType === FileType.Video && (
          <video
            src={file.content}
            controls
            className="h-[230px] w-auto object-cover transition-all"
          >
            <source src={file.content} type={file.type} />
            <track kind="captions" />
          </video>
        )}
        {fileType === FileType.Document && (
          <div className="flex h-[230px] w-[150px] items-center justify-center bg-muted">
            <p>Document with name {file.name}</p>
          </div>
        )}
        {fileType === FileType.Unknown && (
          <div className="flex h-[230px] w-[150px] items-center justify-center bg-muted">
            Unknown file type with name {file.name}
          </div>
        )}
      </div>
      <div className="space-y-1 text-sm">
        <h3 className="font-medium leading-none">{file.name}</h3>
        {fileType !== FileType.Unknown && (
          <p className="text-xs text-muted-foreground">{file.type}</p>
        )}
      </div>
    </div>
  );
}
