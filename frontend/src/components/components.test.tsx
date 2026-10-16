import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Badge } from './Badge'
import { Kpi } from './Kpi'
import { PlateChip } from './PlateChip'
import { ProgressBar } from './ProgressBar'
import { Skeleton } from './Skeleton'

describe('PlateChip', () => {
  it('é lida como "Placa ABC1D23" e a faixa BRASIL fica fora da árvore de acessibilidade', () => {
    render(<PlateChip plate="ABC1D23" />)

    // O leitor de tela ouve "Placa ABC1D23"; o desenho (faixa e letras) fica fora da árvore de acessibilidade.
    expect(screen.getByText('Placa ABC1D23')).toHaveClass('sr-only')
    expect(screen.getByText('BRASIL').closest('[aria-hidden="true"]')).not.toBeNull()
    expect(screen.getByText('ABC1D23', { exact: true }).closest('[aria-hidden="true"]')).not.toBeNull()
  })
})

describe('ProgressBar', () => {
  it('expõe o progresso de 0 a 100 com o nome', () => {
    render(<ProgressBar value={0.9} tone="dueSoon" label="Troca de óleo" />)

    const bar = screen.getByRole('progressbar', { name: 'Troca de óleo' })
    expect(bar).toHaveAttribute('aria-valuenow', '90')
    expect(bar).toHaveAttribute('aria-valuemin', '0')
    expect(bar).toHaveAttribute('aria-valuemax', '100')
  })

  it('limita o valor entre 0 e 100', () => {
    render(<ProgressBar value={1.4} tone="overdue" label="Fluido de freio" />)

    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '100')
  })
})

describe('Badge', () => {
  it('mostra o texto do estado (nunca só a cor)', () => {
    render(<Badge tone="overdue">Vencido</Badge>)

    expect(screen.getByText('Vencido')).toBeInTheDocument()
  })
})

describe('Kpi', () => {
  it('mostra rótulo, valor e dica', () => {
    render(<Kpi label="Custo por km" value="R$ 0,29" hint="em 23.400 km registrados" />)

    expect(screen.getByText('Custo por km')).toBeInTheDocument()
    expect(screen.getByText('R$ 0,29')).toBeInTheDocument()
    expect(screen.getByText('em 23.400 km registrados')).toBeInTheDocument()
  })
})

describe('Skeleton', () => {
  it('é decorativo', () => {
    const { container } = render(<Skeleton className="h-4 w-20" />)

    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
  })
})
