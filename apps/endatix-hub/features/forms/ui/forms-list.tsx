'use client'

import { Form } from "@/types";
import FormCard from "./form-card";
import { useState, useMemo } from "react";
import FormSheet from "./form-sheet";

type FormDataProps = {
    forms: Form[];
};

const FormsList = ({ forms }: FormDataProps) => {
    const [selectedFormId, setSelectedFormId] = useState<string | null>(null);
    const [isSheetOpen, setIsSheetOpen] = useState(false);

    const selectedForm = useMemo(() => forms.find(form => form.id === selectedFormId), [selectedFormId, forms]);

    const handleOnOpenChange = (open: boolean) => {
        setIsSheetOpen(open);
        if (!open) {
            setSelectedFormId(null);
        }
    };

    const handleFormSelected = (formId: string) => {
        setSelectedFormId(formId);
        setIsSheetOpen(true);
    };

    return (
        <>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5 gap-4">
                {forms.map((form) => (
                    <FormCard
                        key={form.id}
                        form={form}
                        isSelected={form.id === selectedFormId}
                        onClick={() => handleFormSelected(form.id)}
                    />
                ))}
            </div>

            <FormSheet
                modal={false}
                open={isSheetOpen}
                onOpenChange={handleOnOpenChange}
                selectedForm={selectedForm ?? null}
            />
        </>
    );
}

export default FormsList;