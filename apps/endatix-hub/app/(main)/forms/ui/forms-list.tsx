'use client';

import { Form } from "@/types";
import FormCard from "./form-card";
import FormSheet from "./form-sheet";
import { useEffect, useState, useMemo } from "react";

type FormDataProps = {
    forms: Form[];
};

const FormsList = ({ forms }: FormDataProps) => {
    const [selectedFormId, setSelectedFormId] = useState<string | null>(null);

    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (!selectedFormId) {
                return;
            }

            if (e.key === "Escape") {
                setSelectedFormId(null); // Deselect
            } else if (selectedFormId) {
                const currentIndex = forms.findIndex(form => form.id === selectedFormId);
                if (e.key === "ArrowUp" || e.key === "ArrowRight") {
                    const prevIndex = (currentIndex > 0 ? currentIndex - 1 : forms.length - 1);
                    setSelectedFormId(forms[prevIndex].id);
                } else if (e.key === "ArrowDown" || e.key === "ArrowLeft") {
                    const nextIndex = (currentIndex < forms.length - 1 ? currentIndex + 1 : 0);
                    setSelectedFormId(forms[nextIndex].id);
                }
            }
        };

        window.addEventListener("keydown", handleKeyDown);
        return () => {
            window.removeEventListener("keydown", handleKeyDown);
        };
    }, [selectedFormId, forms]);

    const selectedForm = useMemo(() => forms.find(form => form.id === selectedFormId), [selectedFormId, forms]);

    return (
        <>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-2 xl:grid-cols-4 gap-4">
                {forms.map((form) => (
                    <FormCard
                        key={form.id}
                        form={form}
                        isSelected={form.id === selectedFormId}
                        onClick={() => setSelectedFormId(form.id)}
                    />
                ))}
            </div>

            {selectedForm && (
                <FormSheet selectedForm={selectedForm} />
            )}
        </>
    );
}

export default FormsList;