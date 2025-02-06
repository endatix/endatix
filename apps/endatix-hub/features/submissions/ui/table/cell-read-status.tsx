import { Badge } from "@/components/ui/badge";
import { SubmissionStatus } from "@/types";
interface CellReadStatusProps {
  code: string;
}

export function CellReadStatus({ code }: CellReadStatusProps) {
  const status = SubmissionStatus.fromCode(code);
  return (
    <Badge variant={status.isNew() ? "default" : "secondary"}>
      {status.label}
    </Badge>
  );
}
