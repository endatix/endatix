"use server";

import { getIronSession, SessionOptions } from "iron-session";
import { cookies } from "next/headers";
import { revalidatePath } from "next/cache";

interface SessionData {
    username: string;
    token: string;
    isLoggedIn: boolean;
}

const defaultSession: SessionData = {
    username: "",
    token: "",
    isLoggedIn: false,
};

const sessionOptions: SessionOptions = {
    password: `${process.env.SESSION_SECRET}`,
    cookieName: "endatix-hub.session",
    cookieOptions: {
        // secure only works in `https` environments
        secure: process.env.NODE_ENV === "production",
    },
};

export const getSession = async () => {
    const session = await getIronSession<SessionData>(cookies(), sessionOptions);

    if (!session.isLoggedIn) {
        session.isLoggedIn = defaultSession.isLoggedIn;
        session.username = defaultSession.username;
        session.token = defaultSession.token;
    }

    return session;
}

export const login = async (token: string, username: string) => {
    const session = await getSession();

    session.token = token;
    session.username = username;
    session.isLoggedIn = true;

    await session.save();

    revalidatePath("/login");
}

//const logout = async () =>  { 
 //   const session = await getSession();
 //   session.destroy();
//
 //   revalidatePath("/login");
 // }