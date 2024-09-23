import { Form } from "@/types";
import FormCard from "./form-card";
import FormSheet from "./form-sheet";

type FormDataProps = {
    data: Form[];
};

const FormsList = ({ data }: FormDataProps) => {
    return (
        <>
            <div className="items-start justify-center gap-4 rounded-lg p-4 md:grid md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                {data.map((form) => (
                    <FormCard form={form} key={form.id} />
                ))}
                {data.map((form) => (
                    <FormCard form={form} key={form.id} />
                ))}
                {data.map((form) => (
                    <FormCard form={form} key={form.id} />
                ))}
                {data.map((form) => (
                    <FormCard form={form} key={form.id} />
                ))}
                {data.map((form) => (
                    <FormCard form={form} key={form.id} />
                ))}
            </div>
            <FormSheet />
        </>
    );
}

export default FormsList;