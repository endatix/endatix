import Image from 'next/image';
import { cn } from '@/lib/utils';
import { File, FileType, getFileType } from '@/lib/questions/file/file-type';
import { FileText, FileX2 } from 'lucide-react';
import Link from 'next/link';

interface FileViewerProps extends React.HTMLAttributes<HTMLDivElement> {
  file: File;
  width?: number;
  height?: number;
  aspectRatio?: 'portrait' | 'square';
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
            <FileText className="h-10 w-10" />
            <Link href={file.content}>Link to file</Link>
          </div>
        )}
        {fileType === FileType.Unknown && (
          <div className="flex h-[230px] w-[150px] items-center justify-center bg-muted">
            <FileX2 className="h-10 w-10" />
            <Link href={file.content}>Link to file</Link>
          </div>
        )}
      </div>
      <div className="space-y-1 text-sm">
        <h3 className="font-medium leading-none">{file.name}</h3>
        {file.type && <p className="text-xs text-muted-foreground">{file.type}</p>}
      </div>
    </div>
  );
}
