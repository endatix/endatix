"use client";

import { showComingSoonMessage } from "@/components/layout-ui/utils/coming-soon-message";
import Link from "next/link";

const NewAccountLink = () => (
    <div className="mt-4 text-center text-sm">
        Don&apos;t have an account?{" "}
        <Link
            onClick={(e) => showComingSoonMessage(e)}
            href="#"
            className="underline">
            Sign up
        </Link>
    </div>
)

export default NewAccountLink;