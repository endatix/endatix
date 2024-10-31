
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuLabel, DropdownMenuSeparator } from "@/components/ui/dropdown-menu"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { BellIcon, CalendarCheck2Icon, InboxIcon, ListTodo } from "lucide-react"


type NotificationBadgeStyle = 'badge' | 'dot';
type ButtonVariant = 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
type ButtonSize = 'default' | 'sm' | 'lg' | 'icon';

interface NotificationsBellProps extends React.HTMLAttributes<HTMLDivElement> {
    bellButtonVariant?: ButtonVariant,
    bellButtonSize?: ButtonSize,
    badgeStyle?: NotificationBadgeStyle,
    renderSampleData: boolean
}

interface NotificationBadgeProps {
    notificationsCount?: number,
    badgeStyle?: NotificationBadgeStyle
}

const NotificationBadge: React.FC<NotificationBadgeProps> = ({
    notificationsCount = 0,
    badgeStyle = 'badge'
}) => {
    const notificationsLabel = notificationsCount > 9 ? '9+' : notificationsCount.toString();
    const paddingClasses = notificationsLabel.length > 1 ? 'px-1 py-0.5' : 'px-1.5 py-0.5 mr-0.5';

    if (notificationsCount <= 0) return null;

    const variants = {
        badge: (
            <Badge
                className={`absolute text-white bg-endatix -top-0 -right-2 rounded-full  text-xs ${paddingClasses}`}
            >
                {notificationsLabel}
            </Badge>
        ),
        dot: (
            <span className="absolute top-1 right-0 h-3 w-3 mr-1 rounded-full bg-endatix"></span>
        ),
    }

    return variants[badgeStyle];
}

const NotificationsBell: React.FC<NotificationsBellProps> = ({
    bellButtonVariant = 'ghost',
    bellButtonSize = 'icon',
    badgeStyle = 'dot',
    renderSampleData,
    className, ...props }: NotificationsBellProps) => {

    const notificationsCount = 11;
    return (
        <div aria-label="notifications-bell" className={className} {...props} >
            <DropdownMenu >
                <DropdownMenuTrigger asChild>
                    <Button variant={bellButtonVariant} size={bellButtonSize} className="relative">
                        <BellIcon className="h-6 w-6 text-muted-foreground" />
                        <NotificationBadge
                            notificationsCount={notificationsCount}
                            badgeStyle={badgeStyle} />
                    </Button>
                </DropdownMenuTrigger>
                {renderSampleData &&
                    <DropdownMenuContent align="end" className="w-80 p-4">
                        <DropdownMenuLabel className="mb-2 text-lg font-medium">Notifications</DropdownMenuLabel>
                        <DropdownMenuSeparator className="my-2" />
                        <div className="space-y-4">
                            <div className="flex items-start gap-3">
                                <div className="flex h-8 w-8 items-center justify-center rounded-full text-endatix bg-secondary">
                                    <ListTodo className="h-5 w-5" />
                                </div>
                                <div className="flex-1 space-y-1">
                                    <p className="text-sm font-medium">New form submission</p>
                                    <p className="text-sm text-gray-500 dark:text-gray-400">5 minutes ago</p>
                                </div>
                            </div>
                            <div className="flex items-start gap-3">
                                <div className="flex h-8 w-8 items-center justify-center rounded-full text-endatix bg-secondary">
                                    <InboxIcon className="h-5 w-5" />
                                </div>
                                <div className="flex-1 space-y-1">
                                    <p className="text-sm font-medium">You have a new message</p>
                                    <p className="text-sm text-gray-500 dark:text-gray-400">1 minute ago</p>
                                </div>
                            </div>
                            <div className="flex items-start gap-3">
                                <div className="flex h-8 w-8 items-center justify-center rounded-full text-endatix bg-secondary">
                                    <CalendarCheck2Icon className="h-5 w-5" />
                                </div>
                                <div className="flex-1 space-y-1">
                                    <p className="text-sm font-medium">RS-200 trial survey is expiring soon</p>
                                    <p className="text-sm text-gray-500 dark:text-gray-400">2 hours ago</p>
                                </div>
                            </div>
                        </div>
                    </DropdownMenuContent>
                }
            </DropdownMenu>
        </div>
    )
}


export default NotificationsBell