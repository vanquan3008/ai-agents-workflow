import { useCallback, useState } from 'react'
import type { DepartmentConfig, DepartmentDraft } from '../types'

interface EditSession {
  /** The edit version this session started from; sent as expectedEditVersion so stale saves get 409. */
  version: number
  /** The server configuration at that version: the baseline that decides whether there are unsaved edits. */
  base: DepartmentConfig
  config: DepartmentConfig
}

/** Canonical form for change detection: the server trims text and orders rooms, which must not count as an edit. */
function canonical(config: DepartmentConfig): string {
  return JSON.stringify({
    name: config.name.trim(),
    theme: config.theme,
    repositoryIds: [...config.repositoryIds].sort(),
    rooms: [...config.rooms]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((r) => [r.roomId, r.label.trim(), r.stage, r.x, r.y, r.width, r.height, r.sortOrder, r.assetPreset, r.action]),
  })
}

export const sameConfig = (a: DepartmentConfig, b: DepartmentConfig) => canonical(a) === canonical(b)

const startSession = (draft: DepartmentDraft): EditSession => ({ version: draft.editVersion, base: draft.config, config: draft.config })

/**
 * Local editing state over the server draft. Unsaved edits are measured against the version the session started
 * from, not against whatever the server holds now. When the server moves to a new version the session follows it if
 * nothing was edited, or if the server now holds exactly the local content (this tab's own save). Otherwise the edits
 * and the original version are kept, so saving reports a conflict instead of silently dropping either side's work.
 */
export function useDraftEditor(draft: DepartmentDraft) {
  const [session, setSession] = useState<EditSession>(() => startSession(draft))
  const dirty = !sameConfig(session.config, session.base)

  // Adjusting state while rendering (instead of in an effect) avoids a frame showing the outdated layout.
  if (session.version !== draft.editVersion && (!dirty || sameConfig(session.config, draft.config))) {
    setSession(startSession(draft))
  }

  const update = useCallback((change: (config: DepartmentConfig) => DepartmentConfig) => {
    setSession((current) => ({ ...current, config: change(current.config) }))
  }, [])

  /** Discards local edits and adopts the given server draft, e.g. after a restore or a conflict reload. */
  const replace = useCallback((next: DepartmentDraft) => setSession(startSession(next)), [])

  return { config: session.config, editVersion: session.version, dirty, update, replace }
}
