import { Workflow } from 'lucide-react'
import { NavLink, useLocation } from 'react-router'
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
import { APPROVALS_PATH, isNavigationItemActive, visibleNavigation } from './navigation'
import { UserMenu } from './UserMenu'

function BrandHeader() {
  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <SidebarMenuButton size="lg" asChild className="hover:bg-transparent active:bg-transparent">
          <NavLink to="/" aria-label={`${env.appName} – Tổng quan`}>
            <span className="bg-primary text-primary-foreground flex size-7 shrink-0 items-center justify-center rounded-md shadow-xs">
              <Workflow className="size-4" />
            </span>
            <span className="grid min-w-0 flex-1 leading-tight">
              <span className="truncate text-sm font-semibold tracking-tight">{env.appName}</span>
              <span className="text-muted-foreground truncate text-[11px]">{env.environment}</span>
            </span>
          </NavLink>
        </SidebarMenuButton>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}

export function AppSidebar() {
  const { isSuperAdmin } = useAuth()
  const { pathname } = useLocation()
  const pendingApprovals = useApprovals('PENDING')
  const badges: Record<string, number> = { [APPROVALS_PATH]: pendingApprovals.data?.totalCount ?? 0 }

  return (
    <Sidebar collapsible="icon" variant="inset">
      <SidebarHeader>
        <BrandHeader />
      </SidebarHeader>

      <SidebarContent>
        {visibleNavigation(isSuperAdmin).map((section) => (
          <SidebarGroup key={section.label}>
            <SidebarGroupLabel className="text-[11px] tracking-wide uppercase">{section.label}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {section.items.map((item) => (
                  <SidebarMenuItem key={item.to}>
                    <SidebarMenuButton asChild isActive={isNavigationItemActive(item, pathname)} tooltip={item.label}>
                      <NavLink to={item.to} end={item.exact}>
                        <item.icon />
                        <span>{item.label}</span>
                      </NavLink>
                    </SidebarMenuButton>
                    {badges[item.to] ? (
                      <SidebarMenuBadge className="bg-warning/15 text-warning rounded-md tabular-nums">{badges[item.to]}</SidebarMenuBadge>
                    ) : null}
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>

      <SidebarFooter>
        <UserMenu />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}
