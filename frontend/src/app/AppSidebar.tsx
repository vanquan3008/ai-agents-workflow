import { Bot, ChevronsUpDown, FolderKanban, KeyRound, LayoutDashboard, ListTodo, LogOut, Monitor, Moon, ShieldCheck, Sun, Users } from 'lucide-react'
import { useTheme } from 'next-themes'
import { NavLink, useLocation, useNavigate } from 'react-router'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from '@/components/ui/sidebar'
import { useApprovals } from '@/features/approvals/api'
import { useAuth } from '@/features/auth/AuthProvider'
import { env } from '@/shared/config/env'
import { initials } from '@/shared/lib/format'

interface NavItem {
  to: string
  label: string
  icon: typeof LayoutDashboard
  end?: boolean
}

const WORK: NavItem[] = [
  { to: '/', label: 'Tổng quan', icon: LayoutDashboard, end: true },
  { to: '/tasks', label: 'Task', icon: ListTodo },
  { to: '/approvals', label: 'Phê duyệt', icon: ShieldCheck },
]

const MANAGE: NavItem[] = [{ to: '/projects', label: 'Project & quy trình', icon: FolderKanban }]

function NavGroup({ label, items, badges = {} }: { label: string; items: NavItem[]; badges?: Record<string, number> }) {
  const location = useLocation()
  const isActive = (item: NavItem) => (item.end ? location.pathname === item.to : location.pathname.startsWith(item.to))

  return (
    <SidebarGroup>
      <SidebarGroupLabel>{label}</SidebarGroupLabel>
      <SidebarGroupContent>
        <SidebarMenu>
          {items.map((item) => (
            <SidebarMenuItem key={item.to}>
              <SidebarMenuButton asChild isActive={isActive(item)} tooltip={item.label}>
                <NavLink to={item.to} end={item.end}>
                  <item.icon />
                  <span>{item.label}</span>
                </NavLink>
              </SidebarMenuButton>
              {badges[item.to] ? <SidebarMenuBadge>{badges[item.to]}</SidebarMenuBadge> : null}
            </SidebarMenuItem>
          ))}
        </SidebarMenu>
      </SidebarGroupContent>
    </SidebarGroup>
  )
}

export function AppSidebar() {
  const { user, isSuperAdmin, logout } = useAuth()
  const { theme, setTheme } = useTheme()
  const navigate = useNavigate()
  const pending = useApprovals('PENDING')

  return (
    <Sidebar collapsible="icon" variant="inset">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <NavLink to="/">
                <div className="bg-primary text-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
                  <Bot className="size-4" />
                </div>
                <div className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-semibold">{env.appName}</span>
                  <span className="text-muted-foreground truncate text-xs">AI bug-fixing pipeline</span>
                </div>
              </NavLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <NavGroup label="Công việc" items={WORK} badges={{ '/approvals': pending.data?.totalCount ?? 0 }} />
        <NavGroup label="Quản lý" items={MANAGE} />
        {isSuperAdmin && <NavGroup label="Quản trị hệ thống" items={[{ to: '/admin/users', label: 'Người dùng', icon: Users }]} />}
      </SidebarContent>

      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton size="lg" className="data-[state=open]:bg-sidebar-accent">
                  <Avatar className="size-8 rounded-lg">
                    <AvatarFallback className="rounded-lg">{initials(user?.displayName ?? '?')}</AvatarFallback>
                  </Avatar>
                  <div className="grid flex-1 text-left text-sm leading-tight">
                    <span className="truncate font-medium">{user?.displayName}</span>
                    <span className="text-muted-foreground truncate text-xs">{user?.email}</span>
                  </div>
                  <ChevronsUpDown className="ml-auto size-4" />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-60" side="top" align="end">
                <DropdownMenuLabel className="font-normal">
                  <div className="text-sm font-medium">{user?.displayName}</div>
                  <div className="text-muted-foreground text-xs">{isSuperAdmin ? 'Super Admin' : 'Người dùng'}</div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={() => void navigate('/change-password')}>
                  <KeyRound /> Đổi mật khẩu
                </DropdownMenuItem>
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    <Sun /> Giao diện
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent>
                    <DropdownMenuRadioGroup value={theme ?? 'system'} onValueChange={setTheme}>
                      <DropdownMenuRadioItem value="light">
                        <Sun /> Sáng
                      </DropdownMenuRadioItem>
                      <DropdownMenuRadioItem value="dark">
                        <Moon /> Tối
                      </DropdownMenuRadioItem>
                      <DropdownMenuRadioItem value="system">
                        <Monitor /> Theo hệ thống
                      </DropdownMenuRadioItem>
                    </DropdownMenuRadioGroup>
                  </DropdownMenuSubContent>
                </DropdownMenuSub>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onSelect={() => void logout()}>
                  <LogOut /> Đăng xuất
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}
