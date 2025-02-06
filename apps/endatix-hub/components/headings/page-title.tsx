import { cn } from "@/lib/utils";

interface PageTitleProps {
  title: string;
  className?: string;
}

const PageTitle = ({ title, className, ...props }: PageTitleProps) => {
  return (
    <h1
      className={cn("text-4xl font-semibold tracking-tight", className)}
      {...props}
    >
      {title}
    </h1>
  );
};

export default PageTitle;
