import { Pencil } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { AdminUser } from '@/shared/api/types'
import { formatNumber, initials } from '@/shared/lib/format'
import { RelativeTime } from '@/shared/ui/relative-time'

export function UserTable({ users, onEdit }: { users: AdminUser[]; onEdit: (user: AdminUser) => void }) {
  return (
    <Table>
      <TableHeader>
        <TableRow className="hover:bg-transparent">
          <TableHead className="w-full min-w-40">Người dùng</TableHead>
          <TableHead>Quyền</TableHead>
          <TableHead>Trạng thái</TableHead>
          <TableHead className="hidden md:table-cell">Đăng nhập gần nhất</TableHead>
          <TableHead className="hidden text-right md:table-cell">Project</TableHead>
          <TableHead className="w-10" aria-label="Thao tác" />
        </TableRow>
      </TableHeader>
      <TableBody>
        {users.map((user) => (
          <TableRow key={user.id}>
            <TableCell className="max-w-0">
              <div className="flex items-center gap-2.5">
                <Avatar className="size-7 rounded-md">
                  <AvatarFallback className="rounded-md text-[11px]">{initials(user.displayName)}</AvatarFallback>
                </Avatar>
                <div className="min-w-0">
                  <p className="truncate font-medium">{user.displayName}</p>
                  <p className="text-muted-foreground truncate text-xs">{user.email}</p>
                </div>
              </div>
            </TableCell>
            <TableCell>
              <Badge variant={user.systemRole === 'SUPER_ADMIN' ? 'default' : 'secondary'} className="rounded-md">
                {user.systemRole === 'SUPER_ADMIN' ? 'Super Admin' : 'User'}
              </Badge>
            </TableCell>
            <TableCell className="text-xs whitespace-nowrap">
              {!user.isActive ? (
                <span className="text-muted-foreground">Đã vô hiệu hóa</span>
              ) : user.mustChangePassword ? (
                <span className="text-warning">Chờ đổi mật khẩu</span>
              ) : (
                <span className="text-success">Hoạt động</span>
              )}
            </TableCell>
            <TableCell className="text-muted-foreground hidden text-xs md:table-cell">
              <RelativeTime value={user.lastLoginAt} />
            </TableCell>
            <TableCell className="text-muted-foreground hidden text-right tabular-nums md:table-cell">{formatNumber(user.projectCount)}</TableCell>
            <TableCell>
              <Button variant="ghost" size="icon-sm" onClick={() => onEdit(user)} aria-label={`Chỉnh sửa ${user.email}`}>
                <Pencil />
              </Button>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
