'use client'

import { FormEditorProps } from "./form-editor"
import dynamic from "next/dynamic"

const FormEditor = dynamic(() => import('./form-editor'), {
    ssr: false,
});

const FormEditorContainer = (props: FormEditorProps) => {
    return (
        <FormEditor {...props} />
    )
}

export default FormEditorContainer;