import {
  Check,
  Eye,
  LucideProps,
  Sparkles,
  Variable,
  XCircle,
} from 'lucide-react';

interface StatusMetadata {
  label: string;
  icon: React.ForwardRefExoticComponent<
    Omit<LucideProps, 'ref'> & React.RefAttributes<SVGSVGElement>
  >;
  color: string;
}

export class SubmissionStatus {
  private static readonly metadata: Record<
    SubmissionStatusType,
    StatusMetadata
  > = {
    new: {
      label: 'New',
      icon: Sparkles,
      color: 'text-blue-500',
    },
    seen: {
      label: 'Seen',
      icon: Eye,
      color: 'text-yellow-500',
    },
    approved: {
      label: 'Approved',
      icon: Check,
      color: 'text-green-500',
    },
    declined: {
      label: 'Declined',
      icon: XCircle,
      color: 'text-red-500',
    },
    unknown: {
      label: 'Unknown',
      icon: Variable,
      color: 'text-gray-500',
    },
  };

  static getMetadata(status: SubmissionStatusType): StatusMetadata {
    return this.metadata[status];
  }

  static fromString(status: string): SubmissionStatusType {
    const foundStatus = Object.values(this.values).find(
      (s) => s === status?.toLowerCase()
    ) as SubmissionStatusType;
    return foundStatus ?? this.values.unknown;
  }

  static readonly values = {
    new: 'new',
    seen: 'seen',
    approved: 'approved',
    declined: 'declined',
    unknown: 'unknown',
  } as const;
}

export type SubmissionStatusType =
  (typeof SubmissionStatus.values)[keyof typeof SubmissionStatus.values];