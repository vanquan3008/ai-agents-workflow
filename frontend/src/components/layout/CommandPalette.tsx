import { useEffect } from 'react'
import { FolderGit2, KeyRound, ListTodo, LogOut, Monitor, Moon, Plus, Sun } from 'lucide-react'
import { useTheme } from 'next-themes'
import { useNavigate } from 'react-router'
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from '@/components/ui/command'
import { useAuth } from '@/features/auth/AuthProvider'
import { useProjects } from '@/features/projects/api'
import { useTasks } from '@/features/tasks/api'
import { StatusDot } from '@/shared/ui/status-badge'
import { visibleNavigation } from './navigation'

interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  const navigate = useNavigate()
  const { setTheme } = useTheme()
  const { isSuperAdmin, logout } = useAuth()
  const projects = useProjects()
  const recentTasks = useTasks({ pageSize: 6 }, { enabled: open, refetchInterval: 0 })

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault()
        onOpenChange(!open)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [open, onOpenChange])

  function run(action: () => void) {
    onOpenChange(false)
    action()
  }

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput placeholder="Tìm trang, project, task hoặc thao tác…" />
      <CommandList>
        <CommandEmpty>Không có kết quả phù hợp.</CommandEmpty>

        <CommandGroup heading="Thao tác">
          <CommandItem value="giao việc mới tạo task" onSelect={() => run(() => void navigate('/tasks?new=1'))}>
            <Plus /> Giao việc mới
          </CommandItem>
          <CommandItem value="đổi mật khẩu" onSelect={() => run(() => void navigate('/change-password'))}>
            <KeyRound /> Đổi mật khẩu
          </CommandItem>
        </CommandGroup>

        <CommandSeparator />
        {visibleNavigation(isSuperAdmin).map((section) => (
          <CommandGroup key={section.label} heading={section.label}>
            {section.items.map((item) => (
              <CommandItem
                key={item.to}
                value={`${item.label} ${(item.keywords ?? []).join(' ')}`}
                onSelect={() => run(() => void navigate(item.to))}
              >
                <item.icon /> {item.label}
              </CommandItem>
            ))}
          </CommandGroup>
        ))}

        {recentTasks.data && recentTasks.data.items.length > 0 && (
          <>
            <CommandSeparator />
            <CommandGroup heading="Task gần đây">
              {recentTasks.data.items.map((task) => (
                <CommandItem
                  key={task.id}
                  value={`task ${task.title} ${task.repositoryFullName} ${task.id}`}
                  onSelect={() => run(() => void navigate(`/tasks/${task.id}`))}
                >
                  <ListTodo />
                  <span className="truncate">{task.title}</span>
                  <CommandShortcut className="flex items-center gap-1.5 tracking-normal">
                    <StatusDot status={task.status} />
                    <span className="hidden font-mono sm:inline">{task.repositoryFullName}</span>
                  </CommandShortcut>
                </CommandItem>
              ))}
            </CommandGroup>
          </>
        )}

        {projects.data && projects.data.length > 0 && (
          <>
            <CommandSeparator />
            <CommandGroup heading="Project">
              {projects.data.map((project) => (
                <CommandItem
                  key={project.id}
                  value={`project ${project.name} ${project.slug}`}
                  onSelect={() => run(() => void navigate(`/projects/${project.id}`))}
                >
                  <FolderGit2 /> {project.name}
                  <CommandShortcut className="font-mono tracking-normal">{project.slug}</CommandShortcut>
                </CommandItem>
              ))}
            </CommandGroup>
          </>
        )}

        <CommandSeparator />
        <CommandGroup heading="Giao diện">
          <CommandItem value="giao diện sáng light" onSelect={() => run(() => setTheme('light'))}>
            <Sun /> Sáng
          </CommandItem>
          <CommandItem value="giao diện tối dark" onSelect={() => run(() => setTheme('dark'))}>
            <Moon /> Tối
          </CommandItem>
          <CommandItem value="giao diện theo hệ thống system" onSelect={() => run(() => setTheme('system'))}>
            <Monitor /> Theo hệ thống
          </CommandItem>
        </CommandGroup>

        <CommandSeparator />
        <CommandGroup heading="Phiên">
          <CommandItem value="đăng xuất logout" onSelect={() => run(() => void logout())}>
            <LogOut /> Đăng xuất
          </CommandItem>
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  )
}
