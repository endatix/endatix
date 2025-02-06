import { Skeleton } from "@/components/ui/skeleton";

export default function Loading() {
  return (
    <div className="flex flex-col space-y-3">
      <Skeleton className="h-24  w-full" />
      <Skeleton className="h-[500px] w-full" />
    </div>
  );
}
