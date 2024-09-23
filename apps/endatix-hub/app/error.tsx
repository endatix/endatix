'use client' // Error boundaries must be Client Components

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { AlertCircle } from 'lucide-react'
import { useEffect } from 'react'

export default function Error({
    error,
    reset,
}: {
    error: Error & { digest?: string }
    reset: () => void
}) {
    useEffect(() => {
        // Log the error to an error reporting service
    }, [error])

    return (
        <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Something went wrong!</AlertTitle>
            <AlertDescription>We are notified on the issue and are working on it. <b>Error details:</b> {error.message}</AlertDescription>
            <Button onClick={reset}>Click to Retry</Button>
        </Alert>
    )
}