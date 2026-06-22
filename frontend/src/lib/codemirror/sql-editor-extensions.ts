import { sql } from '@codemirror/lang-sql'
import { EditorState } from '@codemirror/state'
import { EditorView, lineNumbers } from '@codemirror/view'

import { MAX_SQL_LENGTH } from '@/lib/schemas'

export interface SqlEditorExtensionOptions {
  disabled?: boolean
  maxLength?: number
  accessibility?: {
    labelledBy?: string
    describedBy?: string
    ariaLabel?: string
    ariaInvalid?: boolean
    ariaRequired?: boolean
  }
}

export function createSqlEditorExtensions(options: SqlEditorExtensionOptions = {}) {
  const maxLength = options.maxLength ?? MAX_SQL_LENGTH
  const accessibilityAttributes: Record<string, string> = {
    'aria-multiline': 'true',
  }

  if (options.accessibility?.labelledBy) {
    accessibilityAttributes['aria-labelledby'] = options.accessibility.labelledBy
  }

  if (options.accessibility?.ariaLabel) {
    accessibilityAttributes['aria-label'] = options.accessibility.ariaLabel
  }

  if (options.accessibility?.describedBy) {
    accessibilityAttributes['aria-describedby'] = options.accessibility.describedBy
  }

  if (options.accessibility?.ariaRequired) {
    accessibilityAttributes['aria-required'] = 'true'
  }

  if (options.accessibility?.ariaInvalid) {
    accessibilityAttributes['aria-invalid'] = 'true'
  }

  return [
    lineNumbers(),
    sql(),
    EditorView.lineWrapping,
    EditorView.contentAttributes.of(accessibilityAttributes),
    EditorState.changeFilter.of((transaction) => {
      if (!transaction.docChanged) {
        return true
      }

      return transaction.newDoc.length <= maxLength
    }),
    EditorView.editable.of(!options.disabled),
  ]
}
