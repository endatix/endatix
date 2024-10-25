'use client'

import { useState, useEffect, useRef } from "react"
import { startTransition } from "react"
import { useRouter } from "next/navigation"
import { ICreatorOptions } from "survey-creator-core"
import { SurveyCreatorComponent, SurveyCreator } from "survey-creator-react"
import { updateFormDefinitionJsonAction } from "../app/(main)/forms/[formId]/update-form-definition-json.action"
import { updateFormNameAction } from "@/app/(main)/forms/[formId]/update-form-name.action"
import { updateFormStatusAction } from "@/app/(main)/forms/[formId]/update-form-status.action"
import "survey-core/defaultV2.css"
import "survey-creator-core/survey-creator-core.css"

export interface SurveyCreatorProps {
  formId: string;
  formJson: object | null;
  formName: string;
  formIdLabel: string;
  isEnabled: boolean;
  options?: ICreatorOptions;
}

const defaultCreatorOptions: ICreatorOptions = {
  showLogicTab: true,
  showTranslationTab: true
};

export default function SurveyCreatorWidget(props: SurveyCreatorProps) {
  const [enabled, setEnabled] = useState(props.isEnabled);
  const [isSaving, setIsSaving] = useState(false);
  const router = useRouter();
  const [creator, setCreator] = useState<SurveyCreator | null>(null);
  const [isEditingName, setIsEditingName] = useState(false);
  const [name, setName] = useState(props.formName);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [originalName, setOriginalName] = useState(props.formName);

  useEffect(() => {
    if (!creator) {
        const newCreator = new SurveyCreator(props.options || defaultCreatorOptions);
        newCreator.JSON = props.formJson;
        newCreator.saveSurveyFunc = (no: number, callback: (num: number, status: boolean) => void) => {
        console.log(JSON.stringify(newCreator?.JSON));
        callback(no, true);
        };
        setCreator(newCreator);
    }

  }, [creator, props.formJson, props.options]);

  useEffect(() => {
    if (isEditingName) {
      document.addEventListener('mousedown', handleClickOutside);
    } else {
      document.removeEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside); // Clean up event listener
    };
  }, [isEditingName]);

  const handleSaveAndGoBack = async () => {
    try {
      setIsSaving(true);

      const updatedFormJson = creator?.JSON;

      const result = await updateFormDefinitionJsonAction(props.formId, updatedFormJson);
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

  const handleNameSave = async () => {
    if (name !== originalName) {
      startTransition(async () => {
        await updateFormNameAction(props.formId, name);

        setOriginalName(name);
        setName(name);
      });
    }
    setIsEditingName(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleNameSave();
    } else if (e.key === 'Escape') {
      setName(originalName);
      setIsEditingName(false);
    }
  };

  const handleClickOutside = (e: MouseEvent) => {
    if (inputRef.current && !inputRef.current.contains(e.target as Node)) {
      console.log("Clicked outside, waiting to save name...");
      setTimeout(() => {
        handleNameSave();
      }, 0);
    }
  };

  const toggleEnabled = async (enabled : boolean) => {
    startTransition(async () => {
        await updateFormStatusAction(props.formId, enabled);
    })
  }

  return (
    <div>
        <div className="flex items-center mb-4 w-full">
        <button
            onClick={handleSaveAndGoBack}
            className="mr-4 text-3xl flex items-center"
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
            className="font-bold text-lg mr-16 border border-gray-300 rounded"
            autoFocus
          />
        ) : (
          <span
            className="font-bold text-lg mr-16 hover:border hover:border-gray-300 hover:rounded px-1"
            onClick={() => setIsEditingName(true)} // Click to enable editing
            style={{ cursor: 'text' }} // Change cursor to I-beam (text editor cursor)
          >
            {name}
          </span>
        )}

        <span className="text-gray-600 mr-16">ID: {props.formIdLabel}</span>

        <label className="flex items-center space-x-2">
        <span>Enabled</span>
        <div
            className={`relative inline-flex h-6 w-11 items-center rounded-full transition-all ${enabled ? 'bg-blue-500' : 'bg-gray-300'}`}
            onClick={() => {
                    toggleEnabled(!enabled);
                    setEnabled(!enabled);
                }}
                style={{ cursor: 'pointer' }}
        >
            <span
            className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${enabled ? 'translate-x-6' : 'translate-x-1'}`}
            ></span>
        </div>
        </label>
        </div>
        <div style={{ height: "80vh", width: "100%" }}>
            {creator ? <SurveyCreatorComponent creator={creator} /> : <div>Loading...</div>}
        </div>
    </div>
  );
}
