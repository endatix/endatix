import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';
import { getSession } from "@/lib/auth-service";

export async function middleware(request: NextRequest) {
    const currentSession = await getSession();

    if (!currentSession.isLoggedIn) {
        return NextResponse.redirect(new URL('/login', request.url))
    }
}

// See "Matching Paths" below to learn more
export const config = {
    matcher: [
        '/',
        '/home/:path',
        '/dashboard/:path',
        '/forms/:path',
        '/submissions/:path',
    ],
}