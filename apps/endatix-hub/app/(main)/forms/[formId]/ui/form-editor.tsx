"use client";

import { useState, useEffect, useRef, useTransition, useCallback } from "react";
import { useRouter } from "next/navigation";
import { ICreatorOptions, SurveyCreatorModel, UploadFileEvent } from "survey-creator-core";
import { SurveyCreatorComponent, SurveyCreator } from "survey-creator-react";
import { slk } from "survey-core";
import { updateFormDefinitionJsonAction } from "../update-form-definition-json.action";
import { updateFormNameAction } from "@/app/(main)/forms/[formId]/update-form-name.action";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import "./creator-styles.scss";
import "survey-core/defaultV2.css";
import "survey-creator-core/survey-creator-core.css";
import * as themes from "survey-creator-core/themes";
import { Save } from "lucide-react";

interface FormEditorProps {
  formId: string;
  formJson: object | null;
  formName: string;
  options?: ICreatorOptions;
  slkVal?: string;
}

const defaultCreatorOptions: ICreatorOptions = {
  showPreview: true,
  showJSONEditorTab: true,
  showTranslationTab: true,
  showDesignerTab: true,
  showLogicTab: true,
  themeForPreview: "Default",
};

function FormEditor({
  formJson,
  formId,
  formName,
  options,
  slkVal,
}: FormEditorProps) {
  const [creator, setCreator] = useState<SurveyCreator | null>(null);
  const [isSaving] = useState(false);
  const router = useRouter();
  const [isEditingName, setIsEditingName] = useState(formName === "New Form");
  const [name, setName] = useState(formName);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [originalName, setOriginalName] = useState(formName);
  const [isPending, startTransition] = useTransition();
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

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

  const handleUploadFile = useCallback(async (_: SurveyCreatorModel, options: UploadFileEvent) => {
      const formData = new FormData();
      options.files.forEach(function (file: File) {
        formData.append("file", file);
      });

      fetch("/api/hub/v0/storage/upload", {
        method: "POST",
        body: formData,
        headers: {
          "edx-form-id": formId,
        },
      })
        .then((response) => response.json())
        .then((data) => {
          options.callback(
            "success",
            data.url
          );
        })
        .catch((error) => {
          console.error("Error: ", error);
          options.callback("error", undefined);
        });
    }, [formId]);

  useEffect(() => {
    if (creator) {
      creator.JSON = formJson;
      return;
    }

    if (slkVal) {
      slk(slkVal);
    }

    const newCreator = new SurveyCreator(options || defaultCreatorOptions);

    newCreator.applyCreatorTheme(themes.DefaultLight);
    newCreator.JSON = formJson;
    newCreator.saveSurveyFunc = (
      no: number,
      callback: (num: number, status: boolean) => void
    ) => {
      console.log(JSON.stringify(newCreator?.JSON));
      callback(no, true);
    };
    newCreator.onUploadFile.add(handleUploadFile);

    setCreator(newCreator);
  }, [formJson, options, creator, slkVal, handleUploadFile]);

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
      document.addEventListener("mousedown", handleClickOutside);
    } else {
      document.removeEventListener("mousedown", handleClickOutside);
    }
    return () => {
      document.removeEventListener("mousedown", handleClickOutside); // Clean up event listener
    };
  }, [isEditingName, handleNameSave]);

  useEffect(() => {
    if (creator) {
      creator.onModified.add(() => {
        setHasUnsavedChanges(true);
      });
    }
  }, [creator]);

  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (hasUnsavedChanges) {
        e.preventDefault();
        e.returnValue = ""; // Required for Chrome
      }
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasUnsavedChanges]);

  const handleSaveAndGoBack = () => {
    if (hasUnsavedChanges) {
      const confirm = window.confirm(
        "There are unsaved changes. Are you sure you want to leave?"
      );
      if (confirm) {
        router.push("/forms");
      }
    } else {
      router.push("/forms");
    }
  };

  const saveForm = () => {
    startTransition(async () => {
      const isDraft = false;
      const updatedFormJson = creator?.JSON;
      const result = await updateFormDefinitionJsonAction(
        formId,
        isDraft,
        updatedFormJson
      );
      if (result.success) {
        setHasUnsavedChanges(false);
        toast("Form saved");
      } else {
        throw new Error(result.error);
      }
    });
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      handleNameSave();
    } else if (e.key === "Escape") {
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
              style={{ cursor: "text" }} // Change cursor to I-beam (text editor cursor)
            >
              {name}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {hasUnsavedChanges && (
            <span className="font-bold text-black text-xs border border-black px-2 py-0.5 rounded-full whitespace-nowrap">
              Unsaved changes
            </span>
          )}
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
        {creator ? (
          <SurveyCreatorComponent creator={creator} />
        ) : (
          <div>Loading...</div>
        )}
      </div>
    </>
  );
}
export default FormEditor;
export type { FormEditorProps };
