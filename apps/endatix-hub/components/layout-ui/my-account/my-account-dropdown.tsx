"use server"

import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import { getSession } from "@/lib/auth-service"
import LogoutButton from "./logout-button"
import UserAvatar from "@/components/user/user-avatar"
import Link from 'next/link'

const MyAccountDropdown: React.FC = async () => {
    const sessionData = await getSession();

    return (
        <DropdownMenu>
            <DropdownMenuTrigger aria-label="my-account-dropdown">
                <UserAvatar className="w-9 h-9" isLoggedIn={sessionData.isLoggedIn} userName={sessionData.username} />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
                {sessionData.isLoggedIn ?
                    <LoggedInUserOptions /> :
                    <AnonymousUserOptions />
                }
            </DropdownMenuContent>
        </DropdownMenu>
    )
}

const AnonymousUserOptions = () => (
    <>
        <DropdownMenuLabel>Sign Up</DropdownMenuLabel>
    </>
);

const LoggedInUserOptions = () => (
    <>
        <DropdownMenuLabel>My Account</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem>
            <Link href="/settings/security">Settings</Link>
        </DropdownMenuItem>
        <DropdownMenuItem>Support</DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem>
            <LogoutButton />
        </DropdownMenuItem>
    </>
);

export default MyAccountDropdown;