"use server";

import { getForms } from "@/services/api";
import FormsList from "./ui/forms-list";
import PageTitle from "@/components/headings/page-title";
import { Separator } from "@/components/ui/separator";

const Forms = async () => {
  const forms = await getForms();

  return (
    <>
      <PageTitle title="Forms" />
      <Separator className="my-4" />
      <FormsList data={forms} />
    </>
  );
};

export default Forms;
