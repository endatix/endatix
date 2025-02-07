interface SubmissionsLayoutProps {
  children: React.ReactNode;
}

export default function SubmissionsLayout({
  children,
}: SubmissionsLayoutProps) {
  return (
    <>
      {children}
      <div id="submission-details"></div>
    </>
  );
}
