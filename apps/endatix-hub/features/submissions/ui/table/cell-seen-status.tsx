import { Badge } from "@/components/ui/badge";

const getSeenLabel = (isNew: boolean) => isNew ? "New" : "Seen";

interface CellSeenStatusProps {
    isNew: boolean;
    visible?: boolean;
}

export function CellSeenStatus({
    isNew,
    visible = true
}: CellSeenStatusProps) {
    return (
        visible ?
            <Badge variant={isNew ? "secondary" : "default"}>
                {getSeenLabel(isNew)}
            </Badge> : null
    )
}