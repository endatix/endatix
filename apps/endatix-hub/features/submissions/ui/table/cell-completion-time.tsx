import { getElapsedTimeString } from "@/lib/utils";

interface CellCompletionTimeProps {
  startedAt: Date;
  completedAt: Date;
}

export function CellCompletionTime({
  startedAt,
  completedAt,
}: CellCompletionTimeProps) {
  return <div>{getElapsedTimeString(startedAt, completedAt)}</div>;
}
