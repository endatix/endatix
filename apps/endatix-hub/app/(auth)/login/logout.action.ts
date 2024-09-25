"use server";

import { AuthService } from "@/lib/auth-service";
import { redirect } from 'next/navigation'

export async function logoutAction() {
    const authService = new AuthService();
    await authService.logout();

    redirect("/login");
}