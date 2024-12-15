'use client'

import { useState, useEffect, useRef, useTransition, useCallback } from "react"
import { useRouter } from "next/navigation"
import { ICreatorOptions } from "survey-creator-core"
import { SurveyCreatorComponent, SurveyCreator } from "survey-creator-react"
import { updateFormDefinitionJsonAction } from "../update-form-definition-json.action"
import { updateFormNameAction } from "@/app/(main)/forms/[formId]/update-form-name.action"
import { ICreatorTheme } from "survey-creator-core/typings/creator-theme/creator-themes"
import { Save } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import './creator-styles.scss'

interface FormEditorProps {
  formId: string;
  formJson: object | null;
  formName: string;
  options?: ICreatorOptions;
}

const defaultTheme = {
  "themeName": "default",
  "colorPalette": "light",
  "isPanelless": false,
  "backgroundImage": "",
  "backgroundOpacity": 0,
  "backgroundImageAttachment": "scroll",
  "backgroundImageFit": "cover",
  "cssVariables": {
    "--sjs-corner-radius": "4px",
    "--sjs-base-unit": "8px",
    "--sjs-shadow-small": "0px 1px 2px 0px rgba(0, 0, 0, 0.15)",
    "--sjs-shadow-inner": "inset 0px 1px 2px 0px rgba(0, 0, 0, 0.15)",
    "--sjs-border-default": "rgba(0, 0, 0, 0.16)",
    "--sjs-border-light": "rgba(0, 0, 0, 0.09)",
    "--sjs-general-backcolor": "rgba(255, 255, 255, 1)",
    "--sjs-general-backcolor-dark": "rgba(248, 248, 248, 1)",
    "--sjs-general-backcolor-dim-light": "rgba(249, 249, 249, 1)",
    "--sjs-general-backcolor-dim-dark": "rgba(243, 243, 243, 1)",
    "--sjs-general-forecolor": "rgba(0, 0, 0, 0.91)",
    "--sjs-general-forecolor-light": "rgba(0, 0, 0, 0.45)",
    "--sjs-general-dim-forecolor": "rgba(0, 0, 0, 0.91)",
    "--sjs-general-dim-forecolor-light": "rgba(0, 0, 0, 0.45)",
    "--sjs-secondary-backcolor": "rgba(255, 152, 20, 1)",
    "--sjs-secondary-backcolor-light": "rgba(255, 152, 20, 0.1)",
    "--sjs-secondary-backcolor-semi-light": "rgba(255, 152, 20, 0.25)",
    "--sjs-secondary-forecolor": "rgba(255, 255, 255, 1)",
    "--sjs-secondary-forecolor-light": "rgba(255, 255, 255, 0.25)",
    "--sjs-shadow-small-reset": "0px 0px 0px 0px rgba(0, 0, 0, 0.15)",
    "--sjs-shadow-medium": "0px 2px 6px 0px rgba(0, 0, 0, 0.1)",
    "--sjs-shadow-large": "0px 8px 16px 0px rgba(0, 0, 0, 0.1)",
    "--sjs-shadow-inner-reset": "inset 0px 0px 0px 0px rgba(0, 0, 0, 0.15)",
    "--sjs-border-inside": "rgba(0, 0, 0, 0.16)",
    "--sjs-special-red-forecolor": "rgba(255, 255, 255, 1)",
    "--sjs-special-green": "rgba(25, 179, 148, 1)",
    "--sjs-special-green-light": "rgba(25, 179, 148, 0.1)",
    "--sjs-special-green-forecolor": "rgba(255, 255, 255, 1)",
    "--sjs-special-blue": "rgba(67, 127, 217, 1)",
    "--sjs-special-blue-light": "rgba(67, 127, 217, 0.1)",
    "--sjs-special-blue-forecolor": "rgba(255, 255, 255, 1)",
    "--sjs-special-yellow": "rgba(255, 152, 20, 1)",
    "--sjs-special-yellow-light": "rgba(255, 152, 20, 0.1)",
    "--sjs-special-yellow-forecolor": "rgba(255, 255, 255, 1)",
    "--sjs-article-font-xx-large-textDecoration": "none",
    "--sjs-article-font-xx-large-fontWeight": "700",
    "--sjs-article-font-xx-large-fontStyle": "normal",
    "--sjs-article-font-xx-large-fontStretch": "normal",
    "--sjs-article-font-xx-large-letterSpacing": "0",
    "--sjs-article-font-xx-large-lineHeight": "64px",
    "--sjs-article-font-xx-large-paragraphIndent": "0px",
    "--sjs-article-font-xx-large-textCase": "none",
    "--sjs-article-font-x-large-textDecoration": "none",
    "--sjs-article-font-x-large-fontWeight": "700",
    "--sjs-article-font-x-large-fontStyle": "normal",
    "--sjs-article-font-x-large-fontStretch": "normal",
    "--sjs-article-font-x-large-letterSpacing": "0",
    "--sjs-article-font-x-large-lineHeight": "56px",
    "--sjs-article-font-x-large-paragraphIndent": "0px",
    "--sjs-article-font-x-large-textCase": "none",
    "--sjs-article-font-large-textDecoration": "none",
    "--sjs-article-font-large-fontWeight": "700",
    "--sjs-article-font-large-fontStyle": "normal",
    "--sjs-article-font-large-fontStretch": "normal",
    "--sjs-article-font-large-letterSpacing": "0",
    "--sjs-article-font-large-lineHeight": "40px",
    "--sjs-article-font-large-paragraphIndent": "0px",
    "--sjs-article-font-large-textCase": "none",
    "--sjs-article-font-medium-textDecoration": "none",
    "--sjs-article-font-medium-fontWeight": "700",
    "--sjs-article-font-medium-fontStyle": "normal",
    "--sjs-article-font-medium-fontStretch": "normal",
    "--sjs-article-font-medium-letterSpacing": "0",
    "--sjs-article-font-medium-lineHeight": "32px",
    "--sjs-article-font-medium-paragraphIndent": "0px",
    "--sjs-article-font-medium-textCase": "none",
    "--sjs-article-font-default-textDecoration": "none",
    "--sjs-article-font-default-fontWeight": "400",
    "--sjs-article-font-default-fontStyle": "normal",
    "--sjs-article-font-default-fontStretch": "normal",
    "--sjs-article-font-default-letterSpacing": "0",
    "--sjs-article-font-default-lineHeight": "28px",
    "--sjs-article-font-default-paragraphIndent": "0px",
    "--sjs-article-font-default-textCase": "none",
    "--sjs-general-backcolor-dim": "#fbfbfb",
    "--sjs-primary-backcolor": "#18181b",
    "--sjs-primary-backcolor-dark": "rgba(11, 11, 12, 1)",
    "--sjs-primary-backcolor-light": "rgba(0, 84, 209, 1)",
    "--sjs-primary-forecolor": "rgba(255, 255, 255, 1)",
    "--sjs-primary-forecolor-light": "rgba(255, 255, 255, 0.25)",
    "--sjs-special-red": "rgba(229, 10, 62, 1)",
    "--sjs-special-red-light": "rgba(229, 10, 62, 0.1)"
  },
  "headerView": "basic"
};

const defaultCreatorOptions: ICreatorOptions = {
  showPreview: true,
  showJSONEditorTab: true,
  showTranslationTab: true,
  showDesignerTab: true,
  showLogicTab: true,
  themeForPreview: "Default"
};

function FormEditor({ formJson, formId, formName, options }: FormEditorProps) {
  const [creator, setCreator] = useState<SurveyCreator | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const router = useRouter();
  const [isEditingName, setIsEditingName] = useState(false);
  const [name, setName] = useState(formName);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [originalName, setOriginalName] = useState(formName);
  const [isPending, startTransition] = useTransition();

  const handleNameSave = useCallback(async () => {
    if (name !== originalName) {
      startTransition(async () => {
        await updateFormNameAction(formId, name);

        setOriginalName(name);
        setName(name);
        toast("Form name updated");
      });
    }
    setIsEditingName(false);
  }, [formId, name, originalName, startTransition]);

  useEffect(() => {
    if (creator) {
      creator.JSON = formJson;
      return;
    }

    const newCreator = new SurveyCreator(options || defaultCreatorOptions);
    newCreator.JSON = formJson;
    newCreator.theme = defaultTheme as ICreatorTheme;
    newCreator.saveSurveyFunc = (no: number, callback: (num: number, status: boolean) => void) => {
      console.log(JSON.stringify(newCreator?.JSON));
      callback(no, true);
    };
    setCreator(newCreator);

  }, [formJson, options, creator]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (inputRef.current && !inputRef.current.contains(e.target as Node)) {
        console.log("Clicked outside, waiting to save name...");
        setTimeout(() => {
          handleNameSave();
        }, 0);
      }
    };

    if (isEditingName) {
      document.addEventListener('mousedown', handleClickOutside);
    } else {
      document.removeEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside); // Clean up event listener
    };
  }, [isEditingName, handleNameSave]);

  const handleSaveAndGoBack = async () => {
    try {
      setIsSaving(true);

      const updatedFormJson = creator?.JSON;

      const result = await updateFormDefinitionJsonAction(formId, updatedFormJson);
      if (!result.success) {
        throw new Error(result.error);
      }

      router.push('/forms');
    } catch (error) {
      console.error('Failed to save form', error);
    } finally {
      setIsSaving(false);
    }
  };

  const saveForm = () => {
    startTransition(async () => {
      const updatedFormJson = creator?.JSON;
      const result = await updateFormDefinitionJsonAction(formId, updatedFormJson);
      if (result.success) {
        toast("Form saved");
      } else {
        throw new Error(result.error);
      }
    });
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleNameSave();
    } else if (e.key === 'Escape') {
      setName(originalName);
      setIsEditingName(false);
    }
  };

  return (
    <>
      <div className="flex justify-between items-center mt-0 pt-4 pb-4 sticky top-0 z-50 w-full border-border/40 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="flex w-full items-center gap-8">
          <button
            onClick={handleSaveAndGoBack}
            className="mr-0 text-2xl flex items-center"
            disabled={isSaving}
            style={{ border: "none", background: "transparent" }}
          >
            ←
          </button>

          {isEditingName ? (
            <input
              ref={inputRef}
              value={name}
              onChange={(e) => setName(e.target.value)} // Update the name when typing
              onKeyDown={handleKeyDown} // Handle Enter and Esc key presses
              className="font-bold text-lg border border-gray-300 rounded"
              autoFocus
            />
          ) : (
            <span
              className="font-bold text-lg hover:border hover:border-gray-300 hover:rounded px-1"
              onClick={() => setIsEditingName(true)} // Click to enable editing
              style={{ cursor: 'text' }} // Change cursor to I-beam (text editor cursor)
            >
              {name}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2" >
          <Button
            disabled={isPending}
            onClick={saveForm}
            variant="default"
            size="sm"
            className="h-8 border-dashed"
          >
            <Save className="mr-2 h-4 w-4" />
            Save
          </Button>
        </div>
      </div>
      <div id="creator">
        {creator ? <SurveyCreatorComponent creator={creator} /> : <div>Loading...</div>}
      </div>
    </>
  );
}
export default FormEditor
export type {
  FormEditorProps
};
