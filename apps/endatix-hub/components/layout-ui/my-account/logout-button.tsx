"use client";

import { logoutAction } from "@/app/(auth)/login/logout.action";
import { startTransition } from "react";


const LogoutButton = () => {
    const handleLogout = async () => {
        startTransition(async () => {
            await logoutAction();
        })
    }
    return (
        <div onClick={handleLogout}>
            Logout
        </div>
    );
}

export default LogoutButton;