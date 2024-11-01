"use server"

import { getSession } from '@/lib/auth-service';
import { redirect } from 'next/navigation'

export async function logoutAction() {
    const session = await getSession();
    session.destroy()

    redirect("/login");
}
