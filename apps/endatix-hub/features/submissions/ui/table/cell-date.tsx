import { parseDate } from "@/lib/utils";
import { useMemo } from "react";

interface CellDateProps {
    date: Date;
    visible?: boolean;
}

//TODO: Add a date formatting options
export function CellDate({
    date,
    visible = true
}: CellDateProps) {
    const parsedDate = useMemo(() => {
        return parseDate(date);
    }, [date]);

    if (!parsedDate) {
        return <span className="text-muted-foreground">-</span>;
    }

    return (
        visible ? parsedDate.toLocaleString("en-US") : null
    )
}