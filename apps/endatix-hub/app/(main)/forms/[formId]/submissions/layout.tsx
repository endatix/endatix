interface SubmissionsLayoutProps {
    children: React.ReactNode;
    submission: React.ReactNode;
}

export default function SubmissionsLayout({
    children,
    submission
}: SubmissionsLayoutProps) {
    return (
        <>
            {children}
            <div id="submission-details" >
                {submission}
            </div>
        </>
    );
}