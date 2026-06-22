import { EditorView } from '@codemirror/view'

export function createSqlEditorTheme(isDark: boolean) {
  return EditorView.theme(
    {
      '&': {
        backgroundColor: 'var(--background)',
        color: 'var(--foreground)',
        fontSize: '0.875rem',
        fontFamily: 'var(--font-mono)',
      },
      '.cm-scroller': {
        lineHeight: '1.625',
      },
      '.cm-content': {
        caretColor: 'var(--foreground)',
        minHeight: '11rem',
        padding: '0.75rem 0',
      },
      '.cm-cursor, .cm-dropCursor': {
        borderLeftColor: 'var(--foreground)',
      },
      '&.cm-focused .cm-selectionBackground, .cm-selectionBackground, &.cm-focused .cm-content ::selection': {
        backgroundColor: 'color-mix(in oklab, var(--primary) 18%, transparent)',
      },
      '.cm-gutters': {
        backgroundColor: 'color-mix(in oklab, var(--muted) 55%, transparent)',
        color: 'var(--muted-foreground)',
        border: 'none',
        borderRight: '1px solid var(--border)',
      },
      '.cm-activeLineGutter': {
        backgroundColor: 'color-mix(in oklab, var(--muted) 85%, transparent)',
      },
      '.cm-activeLine': {
        backgroundColor: 'color-mix(in oklab, var(--muted) 35%, transparent)',
      },
      '.cm-lineNumbers .cm-gutterElement': {
        minWidth: '2.75rem',
        padding: '0 0.5rem 0 0.75rem',
      },
      '.cm-line': {
        padding: '0 0.5rem 0 0.25rem',
      },
      '.cm-keyword': {
        color: isDark ? 'oklch(0.82 0.12 250)' : 'oklch(0.42 0.14 250)',
        fontWeight: '600',
      },
      '.cm-string': {
        color: isDark ? 'oklch(0.82 0.08 145)' : 'oklch(0.45 0.12 145)',
      },
      '.cm-number': {
        color: isDark ? 'oklch(0.82 0.1 55)' : 'oklch(0.48 0.12 55)',
      },
      '.cm-comment': {
        color: 'var(--muted-foreground)',
        fontStyle: 'italic',
      },
      '.cm-operator': {
        color: isDark ? 'oklch(0.86 0.04 250)' : 'oklch(0.38 0.04 250)',
      },
      '.cm-builtin': {
        color: isDark ? 'oklch(0.84 0.1 320)' : 'oklch(0.44 0.12 320)',
      },
      '.cm-variableName, .cm-propertyName': {
        color: 'var(--foreground)',
      },
      '.cm-punctuation': {
        color: 'var(--muted-foreground)',
      },
      '&.cm-focused': {
        outline: 'none',
      },
    },
    { dark: isDark },
  )
}
