import { useState } from 'react'
import { Outlet } from 'react-router'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'
import { AppSidebar } from './AppSidebar'
import { AppTopbar } from './AppTopbar'
import { CommandPalette } from './CommandPalette'

export function AppShell() {
  const [commandPaletteOpen, setCommandPaletteOpen] = useState(false)

  return (
    <SidebarProvider>
      <a
        href="#main"
        className="bg-primary text-primary-foreground sr-only z-50 rounded-md px-3 py-2 text-sm focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
      >
        Bỏ qua tới nội dung
      </a>
      <AppSidebar />
      <SidebarInset className="min-w-0">
        <AppTopbar onOpenCommandPalette={() => setCommandPaletteOpen(true)} />
        <main id="main" className="flex-1 px-4 py-6 md:px-6 lg:px-8">
          <div className="mx-auto w-full max-w-[1400px]">
            <Outlet />
          </div>
        </main>
      </SidebarInset>
      <CommandPalette open={commandPaletteOpen} onOpenChange={setCommandPaletteOpen} />
    </SidebarProvider>
  )
}
