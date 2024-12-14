
const getCompletionTime = (startedAt: Date, completedAt: Date): string => {
    if (!startedAt || !completedAt) return "-";
    if (completedAt < startedAt) return "-";

    const diff = new Date(completedAt).getTime() - new Date(startedAt).getTime();
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    const secs = Math.floor((diff % (1000 * 60)) / 1000);

    return `${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

interface CellCompletionTimeProps { 
    startedAt: Date;
    completedAt: Date;
}

export function CellCompletionTime({
    startedAt,
    completedAt
}: CellCompletionTimeProps) {
    return <div>{getCompletionTime(startedAt, completedAt)}</div>;
}