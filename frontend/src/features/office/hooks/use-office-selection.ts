import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'

/** Selected task and room live in the URL (?taskId=&room=) so a selected run can be shared and survives reloads. */
export function useOfficeSelection() {
  const [params, setParams] = useSearchParams()
  const taskId = params.get('taskId') ?? undefined
  const roomId = params.get('room') ?? undefined

  const update = useCallback(
    (changes: { taskId?: string | null; roomId?: string | null }) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          const write = (key: string, value: string | null | undefined) => (value ? next.set(key, value) : next.delete(key))
          if ('taskId' in changes) write('taskId', changes.taskId)
          if ('roomId' in changes) write('room', changes.roomId)
          return next
        },
        { replace: true },
      ),
    [setParams],
  )

  const actions = useMemo(
    () => ({
      // A new task starts at its current station, so the room selection is reset.
      selectTask: (id: string) => update({ taskId: id, roomId: null }),
      selectRoom: (id: string | null) => update({ roomId: id }),
      clearTask: () => update({ taskId: null, roomId: null }),
    }),
    [update],
  )

  return { taskId, roomId, ...actions }
}
