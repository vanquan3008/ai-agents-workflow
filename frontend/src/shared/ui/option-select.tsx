import type { ReactNode } from 'react'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'

export interface SelectOption {
  value: string
  label: ReactNode
  disabled?: boolean
}

interface OptionSelectProps {
  id?: string
  name?: string
  value?: string
  defaultValue?: string
  onValueChange?: (value: string) => void
  options: SelectOption[]
  placeholder?: string
  disabled?: boolean
  required?: boolean
  invalid?: boolean
  size?: 'sm' | 'default'
  className?: string
  'aria-label'?: string
}

/**
 * Radix select driven by a plain option list. Radix forbids empty item values,
 * so "no filter" choices should use a sentinel such as "all".
 */
export function OptionSelect({
  id,
  name,
  value,
  defaultValue,
  onValueChange,
  options,
  placeholder,
  disabled,
  required,
  invalid,
  size = 'default',
  className,
  'aria-label': ariaLabel,
}: OptionSelectProps) {
  return (
    <Select name={name} value={value} defaultValue={defaultValue} onValueChange={onValueChange} disabled={disabled} required={required}>
      <SelectTrigger id={id} size={size} aria-label={ariaLabel} aria-invalid={invalid || undefined} className={cn('w-full', className)}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent position="popper">
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value} disabled={option.disabled}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
