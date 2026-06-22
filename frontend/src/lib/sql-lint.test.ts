import { describe, expect, it } from 'vitest'

import { analyzeSqlLint, isSelectOnlyQuery } from '@/lib/sql-lint'

describe('analyzeSqlLint', () => {
  it('returns no warnings for an empty query', () => {
    expect(analyzeSqlLint('   ')).toEqual({
      appearsSelectOnly: false,
      warnings: [],
    })
  })

  it('marks SELECT and WITH queries as select-only', () => {
    expect(analyzeSqlLint('SELECT * FROM Orders')).toEqual({
      appearsSelectOnly: true,
      warnings: [],
    })

    expect(analyzeSqlLint('WITH recent AS (SELECT 1 AS id) SELECT * FROM recent')).toEqual({
      appearsSelectOnly: true,
      warnings: [],
    })
  })

  it('ignores leading comments when detecting the statement type', () => {
    expect(analyzeSqlLint('-- report\nSELECT * FROM Orders')).toEqual({
      appearsSelectOnly: true,
      warnings: [],
    })

    expect(analyzeSqlLint('/* batch */\nWITH cte AS (SELECT 1) SELECT * FROM cte')).toEqual({
      appearsSelectOnly: true,
      warnings: [],
    })
  })

  it('warns when the first statement is not SELECT or WITH', () => {
    const result = analyzeSqlLint('UPDATE Orders SET active = 1')

    expect(result.appearsSelectOnly).toBe(false)
    expect(result.warnings).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          code: 'non_select_statement',
          keyword: 'UPDATE',
        }),
        expect.objectContaining({
          code: 'blocked_keyword',
          keyword: 'UPDATE',
        }),
      ]),
    )
  })

  it('warns about blocked keywords and security tokens anywhere in the query', () => {
    const result = analyzeSqlLint('SELECT * FROM Orders; DROP TABLE Orders')

    expect(result.appearsSelectOnly).toBe(true)
    expect(result.warnings).toEqual([
      expect.objectContaining({
        code: 'blocked_keyword',
        keyword: 'DROP',
      }),
    ])
  })

  it('warns about backend blocked tokens', () => {
    const result = analyzeSqlLint('SELECT * FROM OPENROWSET(...)')

    expect(result.warnings).toEqual([
      expect.objectContaining({
        code: 'blocked_token',
        keyword: 'openrowset',
      }),
    ])
  })

  it('does not match blocked keywords inside identifiers', () => {
    const result = analyzeSqlLint('SELECT updated_at FROM Orders')

    expect(result.warnings).toEqual([])
  })
})

describe('isSelectOnlyQuery', () => {
  it('returns true only for select-like queries without non-select warnings', () => {
    expect(isSelectOnlyQuery('SELECT 1')).toBe(true)
    expect(isSelectOnlyQuery('DELETE FROM Orders')).toBe(false)
  })
})
