'use client';

import { Form } from "@/types";
import FormCard from "./form-card";
import FormSheet from "./form-sheet";
import { useState} from "react";
import { useRouter } from "next/router";

type FormDataProps = {
    forms: Form[];
};

const FormsList = ({ forms }: FormDataProps) => {
    const [formsList, setFormsList] = useState<Form[]>(forms);
    const [selectedForm, setSelectedForm] = useState<Form | null>(null);

    const handleFormUpdate = async (updatedForm: Form) => {
        const router = useRouter();
        await Promise.resolve(() => router.reload());
    };

    return (
        <>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-2 xl:grid-cols-4 gap-4">
                {formsList.map((form) => (
                    <FormCard
                        key={form.id}
                        form={form}
                        isSelected={form.id === selectedForm?.id}
                        onClick={() => setSelectedForm(form)}
                    />
                ))}
            </div>

            {selectedForm && (
                <FormSheet 
                selectedForm={selectedForm}
                onFormUpdate={handleFormUpdate} />
            )}
        </>
    );
}

export default FormsList;