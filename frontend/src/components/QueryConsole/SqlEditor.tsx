import CodeMirror from '@uiw/react-codemirror'
import { useTheme } from 'next-themes'
import { useMemo } from 'react'
import { AlertTriangle, CheckCircle2 } from 'lucide-react'

import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { createSqlEditorExtensions } from '@/lib/codemirror/sql-editor-extensions'
import { createSqlEditorTheme } from '@/lib/codemirror/sql-editor-theme'
import { MAX_SQL_LENGTH } from '@/lib/schemas'
import { analyzeSqlLint } from '@/lib/sql-lint'
import { cn } from '@/lib/utils'

interface SqlEditorProps {
  id?: string
  labelledBy?: string
  value: string
  onChange: (value: string) => void
  error?: string
  disabled?: boolean
}

export function SqlEditor({
  id = 'sql-editor',
  labelledBy,
  value,
  onChange,
  error,
  disabled = false,
}: SqlEditorProps) {
  const { resolvedTheme } = useTheme()
  const errorId = `${id}-error`
  const countId = `${id}-count`
  const syntaxHelpId = `${id}-syntax-help`
  const lintId = `${id}-lint`
  const lint = analyzeSqlLint(value)
  const describedBy = [syntaxHelpId, lint.warnings.length > 0 ? lintId : null, error ? errorId : null, countId]
    .filter(Boolean)
    .join(' ')

  const isDark = resolvedTheme === 'dark'
  const extensions = useMemo(
    () =>
      createSqlEditorExtensions({
        disabled,
        accessibility: {
          labelledBy: labelledBy ?? `${id}-label`,
          describedBy,
          ariaInvalid: Boolean(error),
          ariaRequired: true,
        },
      }),
    [disabled, describedBy, error, id, labelledBy],
  )
  const theme = useMemo(() => createSqlEditorTheme(isDark), [isDark])

  return (
    <div className="space-y-3">
      {labelledBy ? null : <Label id={`${id}-label`}>SQL query</Label>}

      <div className="flex flex-wrap items-center gap-2">
        {lint.appearsSelectOnly ? (
          <Badge variant="secondary" className="gap-1.5 font-normal">
            <CheckCircle2 className="size-3.5" aria-hidden="true" />
            SELECT query
          </Badge>
        ) : value.trim() ? (
          <Badge variant="outline" className="gap-1.5 border-warning/40 bg-warning/10 font-normal text-foreground">
            <AlertTriangle className="size-3.5 text-warning" aria-hidden="true" />
            Review statement type
          </Badge>
        ) : null}
        <p id={syntaxHelpId} className="sr-only">
          SQL editor with T-SQL syntax highlighting and line numbers. Only SELECT statements are accepted by the query
          engine.
        </p>
      </div>

      <div
        className={cn(
          'overflow-hidden rounded-lg border border-input bg-background shadow-xs transition-colors',
          error && 'border-destructive',
          disabled && 'opacity-70',
        )}
      >
        <CodeMirror
          value={value}
          height="11rem"
          theme={theme}
          extensions={extensions}
          onChange={onChange}
          editable={!disabled}
          basicSetup={{
            lineNumbers: false,
            foldGutter: false,
            highlightActiveLine: true,
            highlightActiveLineGutter: true,
            dropCursor: false,
            allowMultipleSelections: false,
            indentOnInput: false,
            bracketMatching: true,
            closeBrackets: false,
            autocompletion: false,
            rectangularSelection: false,
            crosshairCursor: false,
            highlightSelectionMatches: false,
            searchKeymap: false,
          }}
          placeholder="SELECT * FROM Orders WHERE id = @id"
        />
      </div>

      {lint.warnings.length > 0 ? (
        <div
          id={lintId}
          role="status"
          aria-live="polite"
          className="flex gap-3 rounded-lg border border-warning/40 bg-warning/10 px-4 py-3 text-sm text-foreground"
        >
          <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden="true" />
          <div className="space-y-1">
            {lint.warnings.map((warning) => (
              <p key={`${warning.code}-${warning.keyword}`}>{warning.message}</p>
            ))}
          </div>
        </div>
      ) : null}

      <div className="flex items-start justify-between gap-4">
        {error ? (
          <p id={errorId} className="text-sm text-destructive" role="alert" aria-live="polite">
            {error}
          </p>
        ) : (
          <span />
        )}
        <p id={countId} className="shrink-0 text-sm text-muted-foreground" aria-live="polite">
          {value.length.toLocaleString()} / {MAX_SQL_LENGTH.toLocaleString()} characters
        </p>
      </div>
    </div>
  )
}
