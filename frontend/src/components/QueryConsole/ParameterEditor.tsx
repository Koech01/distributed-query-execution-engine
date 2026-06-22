import { Plus, Trash2 } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import type { QueryParameterDto } from '@/components/types'
import { MAX_PARAMETERS } from '@/lib/schemas'

import { COMMON_PARAMETER_TYPES } from './constants'

export interface ParameterFieldErrors {
  name?: string
  type?: string
  value?: string
}

interface ParameterEditorProps {
  parameters: QueryParameterDto[]
  onChange: (parameters: QueryParameterDto[]) => void
  errors?: Record<number, ParameterFieldErrors>
  disabled?: boolean
}

function createEmptyParameter(): QueryParameterDto {
  return {
    name: '@',
    type: 'int',
    value: '',
  }
}

export function ParameterEditor({ parameters, onChange, errors = {}, disabled = false }: ParameterEditorProps) {
  const addParameter = () => {
    if (parameters.length >= MAX_PARAMETERS) {
      return
    }

    onChange([...parameters, createEmptyParameter()])
  }

  const removeParameter = (index: number) => {
    onChange(parameters.filter((_, currentIndex) => currentIndex !== index))
  }

  const updateParameter = (index: number, field: keyof QueryParameterDto, value: string) => {
    onChange(
      parameters.map((parameter, currentIndex) =>
        currentIndex === index ? { ...parameter, [field]: value } : parameter,
      ),
    )
  }

  return (
    <div className="space-y-4">
      <p className="text-sm leading-relaxed text-muted-foreground">
        Use names like <code className="rounded-md bg-muted/60 px-1.5 py-0.5 font-mono text-xs">@id</code> and SQL
        types such as{' '}
        {COMMON_PARAMETER_TYPES.map((type) => (
          <code key={type} className="mr-1 rounded-md bg-muted/60 px-1.5 py-0.5 font-mono text-xs">
            {type}
          </code>
        ))}
      </p>

      {parameters.length === 0 ? (
        <p className="rounded-xl border border-dashed border-border/60 bg-muted/20 px-4 py-6 text-center text-sm text-muted-foreground">
          No parameters added. Use parameters when your SQL references named placeholders.
        </p>
      ) : (
        <ul className="space-y-3">
          {parameters.map((parameter, index) => {
            const rowErrors = errors[index] ?? {}
            const nameErrorId = `parameter-${index}-name-error`
            const typeErrorId = `parameter-${index}-type-error`
            const valueErrorId = `parameter-${index}-value-error`

            return (
              <li key={`parameter-${index}`} className="rounded-xl border border-border/60 bg-background/80 p-4 shadow-xs">
                <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.2fr)_auto] md:items-end">
                  <div className="space-y-2">
                    <Label htmlFor={`parameter-${index}-name`}>Name</Label>
                    <Input
                      id={`parameter-${index}-name`}
                      value={parameter.name}
                      onChange={(event) => updateParameter(index, 'name', event.target.value)}
                      disabled={disabled}
                      aria-required="true"
                      aria-invalid={rowErrors.name ? 'true' : undefined}
                      aria-describedby={rowErrors.name ? nameErrorId : undefined}
                      placeholder="@id"
                    />
                    {rowErrors.name ? (
                      <p id={nameErrorId} className="text-sm text-destructive" role="alert">
                        {rowErrors.name}
                      </p>
                    ) : null}
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor={`parameter-${index}-type`}>Type</Label>
                    <Select
                      value={parameter.type}
                      onValueChange={(value) => updateParameter(index, 'type', value)}
                      disabled={disabled}
                    >
                      <SelectTrigger
                        id={`parameter-${index}-type`}
                        aria-required="true"
                        aria-invalid={rowErrors.type ? 'true' : undefined}
                        aria-describedby={rowErrors.type ? typeErrorId : undefined}
                      >
                        <SelectValue placeholder="Select type" />
                      </SelectTrigger>
                      <SelectContent>
                        {COMMON_PARAMETER_TYPES.map((type) => (
                          <SelectItem key={type} value={type}>
                            {type}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {rowErrors.type ? (
                      <p id={typeErrorId} className="text-sm text-destructive" role="alert">
                        {rowErrors.type}
                      </p>
                    ) : null}
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor={`parameter-${index}-value`}>Value</Label>
                    <Input
                      id={`parameter-${index}-value`}
                      value={parameter.value}
                      onChange={(event) => updateParameter(index, 'value', event.target.value)}
                      disabled={disabled}
                      aria-required="true"
                      aria-invalid={rowErrors.value ? 'true' : undefined}
                      aria-describedby={rowErrors.value ? valueErrorId : undefined}
                      placeholder="42"
                    />
                    {rowErrors.value ? (
                      <p id={valueErrorId} className="text-sm text-destructive" role="alert">
                        {rowErrors.value}
                      </p>
                    ) : null}
                  </div>

                  <Button
                    type="button"
                    variant="outline"
                    size="icon"
                    onClick={() => removeParameter(index)}
                    disabled={disabled}
                    aria-label={`Remove parameter ${index + 1}`}
                  >
                    <Trash2 className="size-4" aria-hidden="true" />
                  </Button>
                </div>
              </li>
            )
          })}
        </ul>
      )}

      <Button
        type="button"
        variant="outline"
        onClick={addParameter}
        disabled={disabled || parameters.length >= MAX_PARAMETERS}
        className="gap-2"
      >
        <Plus className="size-4" aria-hidden="true" />
        Add parameter
      </Button>
    </div>
  )
}
