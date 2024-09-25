"use server";

import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import AvatarIcon from "./avatar-icon"
import LogoutButton from "./logout-button"
import { getSession } from "@/lib/auth-service";

const MyAccountDropdown = async () => {
    const sessionData = await getSession();

    return (
        <DropdownMenu>
            <DropdownMenuTrigger>
                <AvatarIcon />
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
        <DropdownMenuItem>Settings</DropdownMenuItem>
        <DropdownMenuItem>Support</DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem>
            <LogoutButton />
        </DropdownMenuItem>
    </>
);

export default MyAccountDropdown;