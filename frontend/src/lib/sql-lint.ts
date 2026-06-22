export const BLOCKED_STATEMENT_KEYWORDS = [
  'INSERT',
  'UPDATE',
  'DELETE',
  'DROP',
  'TRUNCATE',
  'MERGE',
  'EXEC',
  'EXECUTE',
  'ALTER',
  'CREATE',
  'GRANT',
  'REVOKE',
  'DENY',
] as const

export const BLOCKED_TOKENS = [
  'xp_cmdshell',
  'openrowset',
  'opendatasource',
  'openquery',
  'bulk insert',
] as const

export type SqlLintWarningCode = 'non_select_statement' | 'blocked_keyword' | 'blocked_token'

export interface SqlLintWarning {
  code: SqlLintWarningCode
  message: string
  keyword: string
}

export interface SqlLintResult {
  appearsSelectOnly: boolean
  warnings: SqlLintWarning[]
}

function stripSqlComments(sql: string): string {
  let result = ''
  let index = 0

  while (index < sql.length) {
    const blockCommentStart = sql.indexOf('/*', index)
    const lineCommentStart = sql.indexOf('--', index)

    if (blockCommentStart === -1 && lineCommentStart === -1) {
      result += sql.slice(index)
      break
    }

    const nextSpecial =
      blockCommentStart === -1
        ? lineCommentStart
        : lineCommentStart === -1
          ? blockCommentStart
          : Math.min(blockCommentStart, lineCommentStart)

    result += sql.slice(index, nextSpecial)

    if (nextSpecial === blockCommentStart) {
      const blockCommentEnd = sql.indexOf('*/', blockCommentStart + 2)
      if (blockCommentEnd === -1) {
        break
      }

      index = blockCommentEnd + 2
      continue
    }

    const lineBreak = sql.indexOf('\n', lineCommentStart + 2)
    if (lineBreak === -1) {
      break
    }

    result += '\n'
    index = lineBreak + 1
  }

  return result
}

function getLeadingKeyword(sql: string): string | null {
  const normalized = stripSqlComments(sql).trim()
  if (!normalized) {
    return null
  }

  const match = normalized.match(/^([A-Za-z_][A-Za-z0-9_]*)/)
  return match?.[1]?.toUpperCase() ?? null
}

function containsWholeToken(sql: string, token: string): boolean {
  const pattern = new RegExp(`(^|[^A-Za-z0-9_])(${escapeRegExp(token)})(?![A-Za-z0-9_])`, 'i')
  return pattern.test(sql)
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

export function analyzeSqlLint(sql: string): SqlLintResult {
  const trimmed = sql.trim()

  if (!trimmed) {
    return {
      appearsSelectOnly: false,
      warnings: [],
    }
  }

  const warnings: SqlLintWarning[] = []
  const leadingKeyword = getLeadingKeyword(trimmed)
  const appearsSelectOnly = leadingKeyword === 'SELECT' || leadingKeyword === 'WITH'

  if (leadingKeyword && leadingKeyword !== 'SELECT' && leadingKeyword !== 'WITH') {
    warnings.push({
      code: 'non_select_statement',
      keyword: leadingKeyword,
      message: `Only SELECT statements are accepted. Detected "${leadingKeyword}" at the start of the query.`,
    })
  }

  for (const keyword of BLOCKED_STATEMENT_KEYWORDS) {
    if (containsWholeToken(trimmed, keyword)) {
      warnings.push({
        code: 'blocked_keyword',
        keyword,
        message: `"${keyword}" is not permitted in this query engine.`,
      })
    }
  }

  for (const token of BLOCKED_TOKENS) {
    if (containsWholeToken(trimmed, token)) {
      warnings.push({
        code: 'blocked_token',
        keyword: token,
        message: `"${token}" is blocked for security reasons.`,
      })
    }
  }

  const dedupedWarnings = warnings.filter(
    (warning, index, allWarnings) =>
      allWarnings.findIndex((candidate) => candidate.code === warning.code && candidate.keyword === warning.keyword) === index,
  )

  return {
    appearsSelectOnly: appearsSelectOnly && !dedupedWarnings.some((warning) => warning.code === 'non_select_statement'),
    warnings: dedupedWarnings,
  }
}

export function isSelectOnlyQuery(sql: string): boolean {
  return analyzeSqlLint(sql).appearsSelectOnly
}
