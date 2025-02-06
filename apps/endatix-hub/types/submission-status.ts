import {
  Check,
  Eye,
  LucideProps,
  Sparkles,
  Variable,
  XCircle,
} from "lucide-react";

enum SubmissionStatusKind {
  New = "new",
  Read = "read",
  Approved = "approved",
  Declined = "declined",
  Unknown = "unknown",
}

interface StatusMetadata {
  label: string;
  icon: React.ForwardRefExoticComponent<
    Omit<LucideProps, "ref"> & React.RefAttributes<SVGSVGElement>
  >;
  color: string;
}

class SubmissionStatus {
  private static readonly metadata: Record<
    SubmissionStatusKind,
    StatusMetadata
  > = {
    [SubmissionStatusKind.New]: {
      label: "New",
      icon: Sparkles,
      color: "text-blue-500",
    },
    [SubmissionStatusKind.Read]: {
      label: "Read",
      icon: Eye,
      color: "text-yellow-500",
    },
    [SubmissionStatusKind.Approved]: {
      label: "Approved",
      icon: Check,
      color: "text-green-500",
    },
    [SubmissionStatusKind.Declined]: {
      label: "Declined",
      icon: XCircle,
      color: "text-red-500",
    },
    [SubmissionStatusKind.Unknown]: {
      label: "Unknown",
      icon: Variable,
      color: "text-gray-500",
    },
  } as const;

  private constructor(private readonly kind: SubmissionStatusKind) {}

  // Factory methods
  static new(): SubmissionStatus {
    return new SubmissionStatus(SubmissionStatusKind.New);
  }

  static fromCode(code: string): SubmissionStatus {
    if (!code) {
      return new SubmissionStatus(SubmissionStatusKind.Unknown);
    }

    const normalizedCode = code.toLowerCase();
    const matchedStatus = Object.values(SubmissionStatusKind).find(
      (s) => s.toLowerCase() === normalizedCode,
    );

    return new SubmissionStatus(
      matchedStatus
        ? (matchedStatus as SubmissionStatusKind)
        : SubmissionStatusKind.Unknown,
    );
  }

  // Instance methods
  get label(): string {
    return SubmissionStatus.metadata[this.kind].label;
  }

  get icon(): StatusMetadata["icon"] {
    return SubmissionStatus.metadata[this.kind].icon;
  }

  get color(): string {
    return SubmissionStatus.metadata[this.kind].color;
  }

  get value(): SubmissionStatusKind {
    return this.kind;
  }

  equals(other: SubmissionStatus): boolean {
    return this.kind === other.kind;
  }

  isNew(): boolean {
    return this.kind === SubmissionStatusKind.New;
  }

  isRead(): boolean {
    return this.kind === SubmissionStatusKind.Read;
  }

  isApproved(): boolean {
    return this.kind === SubmissionStatusKind.Approved;
  }

  isDeclined(): boolean {
    return this.kind === SubmissionStatusKind.Declined;
  }

  isUnknown(): boolean {
    return this.kind === SubmissionStatusKind.Unknown;
  }
}

export { SubmissionStatus, SubmissionStatusKind, type StatusMetadata };
