"use client";

import { showComingSoonMessage } from "@/components/layout-ui/teasers/coming-soon-link";
import Link from "next/link";

const NewAccountLink = () => (
  <div className="mt-4 text-center text-sm">
    Don&apos;t have an account?{" "}
    <Link
      onClick={() => showComingSoonMessage()}
      href="#"
      className="underline"
    >
      Sign up
    </Link>
  </div>
);

export default NewAccountLink;
