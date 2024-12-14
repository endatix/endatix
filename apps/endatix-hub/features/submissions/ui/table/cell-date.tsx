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
        try {
            if (!date){
                return null;
            }
            
            const dateValue = date instanceof Date ? date : new Date(date);
            return isNaN(dateValue.getTime()) ? null : dateValue;
        } catch {
            return null;
        }
    }, [date]);

    if (!parsedDate) {
        return <span className="text-muted-foreground">-</span>;
    }

    return (
        visible ? parsedDate.toLocaleString("en-US") : null
    )
}