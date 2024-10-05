"use server";

import { JWTPayload, SignJWT, jwtVerify } from "jose";
import { getIronSession, SessionOptions } from "iron-session";
import { cookies } from "next/headers";
import { revalidatePath } from "next/cache";
import { decodeJwt } from "jose";

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

const securityOptions = {
    secretKey: new TextEncoder().encode(`${process.env.SESSION_SECRET}`)
};

export const encryptToken = async (payload: JWTPayload) => {
    return await new SignJWT(payload)
        .setProtectedHeader({ alg: "HS256" })
        .setIssuedAt()
        .setExpirationTime("10 sec from now")
        .sign(securityOptions.secretKey);
}

export const decryptToken = async (token: string): Promise<JWTPayload> => {
    const { payload } = await jwtVerify(token, securityOptions.secretKey, {
        algorithms: ["HS256"],
    });
    return payload;
}

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

    const jwtToken = decodeJwt(token);
    if (!jwtToken.exp) {
        return;
    }

    const expirationDate = new Date(jwtToken.exp * 1000)
    console.log(`Expiration date is ${expirationDate}`);
    
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