import { Column } from "@tanstack/react-table";

interface ColumnHeaderProps<TData, TValue>
  extends React.HTMLAttributes<HTMLDivElement> {
  column: Column<TData, TValue>;
  title: string;
  visible?: boolean;
}

export function ColumnHeader<TData, TValue>({
  column,
  title,
  visible = true,
  className,
}: ColumnHeaderProps<TData, TValue>) {
  if (column.getIsSorted()) {
    console.debug("Is sorted", column.getIsSorted());
  }
  return (
    <div className={className}>
      {visible ? title : <span className="sr-only">{title}</span>}
    </div>
  );
}
