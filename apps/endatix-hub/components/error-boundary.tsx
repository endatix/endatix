import React, { Component, ErrorInfo } from 'react';
import { AlertCircle } from "lucide-react"
import {
    Alert,
    AlertDescription,
    AlertTitle,
} from "@/components/ui/alert"

interface Props {
    children: React.ReactNode;
}

interface State {
    hasError: boolean;
    errorMessage?: string;
}

class ErrorBoundary extends Component<Props, State> {
    constructor(props: Props) {
        super(props);
        this.state = {
            hasError: false
        };
    }

    componentDidCatch(error: Error, errorInfo: ErrorInfo) {
        console.log(errorInfo);
        this.setState({
            hasError: true,
            errorMessage: error.message
        });
    }

    render() {
        if (this.state.hasError) {
            return (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Error</AlertTitle>
                    {this.state.errorMessage &&
                        <AlertDescription>
                            {this.state.errorMessage}
                        </AlertDescription>
                    }
                </Alert>
            );
        }

        return this.props.children;
    }
}

export default ErrorBoundary;