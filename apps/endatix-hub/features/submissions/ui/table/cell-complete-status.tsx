import { Badge } from "@/components/ui/badge";

const getStatusLabel = (isComplete: boolean) => isComplete ? "Yes" : "No";

interface CellCompleteStatusProps {
    isComplete: boolean;
}

export function CellCompleteStatus({
    isComplete
}: CellCompleteStatusProps) {
    return (
        <Badge variant="outline">
            {getStatusLabel(isComplete)}
        </Badge>
    )
}