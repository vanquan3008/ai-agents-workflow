import { useState, type FormEvent } from 'react'
import { UserPlus } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/shared/api/http'
import type { ProjectRole } from '@/shared/api/types'
import { PROJECT_ROLES, roleDescription, roleLabel } from '@/shared/lib/labels'
import { OptionSelect } from '@/shared/ui/option-select'
import { FormError } from '@/shared/ui/states'
import { useUpsertMember } from '../api'

export function AddMemberForm({ projectId }: { projectId: string }) {
  const upsert = useUpsertMember(projectId)
  const [role, setRole] = useState<ProjectRole>('REQUESTER')
  const emailError = upsert.error instanceof ApiError ? upsert.error.field('email') : undefined

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const email = String(new FormData(form).get('email')).trim()
    upsert.mutate(
      { email, role },
      {
        onSuccess: () => {
          form.reset()
          toast.success(`Đã cập nhật quyền cho ${email}.`)
        },
      },
    )
  }

  return (
    <form onSubmit={submit} className="grid gap-3">
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_14rem_auto] sm:items-end">
        <Field data-invalid={Boolean(emailError) || undefined}>
          <FieldLabel htmlFor="member-email">Email tài khoản</FieldLabel>
          <Input id="member-email" name="email" type="email" required placeholder="dev@company.com" />
        </Field>
        <Field>
          <FieldLabel htmlFor="member-role">Vai trò</FieldLabel>
          <OptionSelect
            id="member-role"
            value={role}
            onValueChange={(value) => setRole(value as ProjectRole)}
            options={PROJECT_ROLES.map((value) => ({ value, label: roleLabel(value) }))}
          />
        </Field>
        <Button type="submit" disabled={upsert.isPending}>
          <UserPlus /> Thêm / đổi vai trò
        </Button>
      </div>
      <p className="text-muted-foreground text-xs">
        <span className="text-foreground font-medium">{roleLabel(role)}:</span> {roleDescription(role)}
      </p>
      <FieldError>{emailError}</FieldError>
      <FormError error={emailError ? null : upsert.error} />
    </form>
  )
}
