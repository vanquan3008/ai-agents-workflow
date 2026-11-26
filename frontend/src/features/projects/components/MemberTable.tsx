import { UserMinus } from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { ProjectMember } from '@/shared/api/types'
import { initials } from '@/shared/lib/format'
import { roleLabel } from '@/shared/lib/labels'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { RelativeTime } from '@/shared/ui/relative-time'
import { FormError } from '@/shared/ui/states'
import { useRemoveMember } from '../api'

export function MemberTable({ projectId, members, canManage }: { projectId: string; members: ProjectMember[]; canManage: boolean }) {
  const remove = useRemoveMember(projectId)

  return (
    <div className="space-y-3">
      <div className="overflow-x-auto">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className="w-full min-w-40">Người dùng</TableHead>
              <TableHead>Vai trò</TableHead>
              <TableHead className="hidden sm:table-cell">Tham gia</TableHead>
              {canManage && <TableHead className="w-10" aria-label="Thao tác" />}
            </TableRow>
          </TableHeader>
          <TableBody>
            {members.map((member) => (
              <TableRow key={member.userId}>
                <TableCell className="max-w-0">
                  <div className="flex items-center gap-2.5">
                    <Avatar className="size-7 rounded-md">
                      <AvatarFallback className="rounded-md text-[11px]">{initials(member.displayName)}</AvatarFallback>
                    </Avatar>
                    <div className="min-w-0">
                      <p className="truncate font-medium">{member.displayName}</p>
                      <p className="text-muted-foreground truncate text-xs">{member.email}</p>
                    </div>
                  </div>
                </TableCell>
                <TableCell>
                  <Badge variant={member.role === 'OWNER' ? 'default' : 'secondary'} className="rounded-md">
                    {roleLabel(member.role)}
                  </Badge>
                </TableCell>
                <TableCell className="text-muted-foreground hidden text-xs sm:table-cell">
                  <RelativeTime value={member.addedAt} />
                </TableCell>
                {canManage && (
                  <TableCell>
                    <ConfirmDialog
                      trigger={
                        <Button variant="ghost" size="icon-sm" disabled={remove.isPending} aria-label={`Gỡ ${member.email}`}>
                          <UserMinus />
                        </Button>
                      }
                      title="Gỡ thành viên?"
                      description={`${member.displayName} (${member.email}) sẽ mất quyền truy cập project này ngay lập tức.`}
                      confirmLabel="Gỡ khỏi project"
                      destructive
                      onConfirm={() => remove.mutate(member.userId, { onSuccess: () => toast.success(`Đã gỡ ${member.email}.`) })}
                    />
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <FormError error={remove.error} />
    </div>
  )
}
