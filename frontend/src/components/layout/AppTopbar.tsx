import { Search } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Kbd } from '@/components/ui/kbd'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { Breadcrumbs } from './Breadcrumbs'
import { ThemeToggle } from './ThemeToggle'

const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.userAgent)

export function AppTopbar({ onOpenCommandPalette }: { onOpenCommandPalette: () => void }) {
  return (
    <header className="bg-background/85 supports-backdrop-filter:bg-background/70 sticky top-0 z-20 flex h-12 shrink-0 items-center gap-2 border-b px-3 backdrop-blur md:rounded-t-xl md:px-4">
      <SidebarTrigger className="-ml-1" />
      <Separator orientation="vertical" className="mr-1 data-[orientation=vertical]:h-4" />
      <Breadcrumbs />
      <div className="ml-auto flex items-center gap-1.5">
        <Button
          variant="outline"
          size="sm"
          onClick={onOpenCommandPalette}
          className="text-muted-foreground hidden w-56 justify-start gap-2 font-normal sm:inline-flex"
        >
          <Search />
          <span className="flex-1 text-left">Tìm kiếm…</span>
          <Kbd>{isMac ? '⌘' : 'Ctrl'}</Kbd>
          <Kbd>K</Kbd>
        </Button>
        <Button variant="ghost" size="icon-sm" onClick={onOpenCommandPalette} className="sm:hidden" aria-label="Mở bảng lệnh">
          <Search />
        </Button>
        <ThemeToggle />
      </div>
    </header>
  )
}
