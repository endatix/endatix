"use client";

import dynamic from "next/dynamic";
import { FormEditorProps } from "./form-editor";
import "./creator-styles.scss";

const FormEditor = dynamic(() => import("./form-editor"), {
  ssr: false,
});

const FormEditorContainer = (props: FormEditorProps) => {
  return <FormEditor {...props} />;
};

export default FormEditorContainer;
