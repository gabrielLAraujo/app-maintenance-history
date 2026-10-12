import type { components } from '../api/schema'

type Schemas = components['schemas']

// Record<enum, string>: se a API ganhar um valor novo, o tsc aponta aqui o rótulo que falta.

export const vehicleTypeLabels: Record<Schemas['VehicleType'], string> = {
  car: 'Carro',
  motorcycle: 'Moto',
  truck: 'Caminhão',
}

export const recordTypeLabels: Record<Schemas['RecordType'], string> = {
  maintenance: 'Manutenção',
  modification: 'Modificação',
}

export const categoryLabels: Record<Schemas['Category'], string> = {
  engine: 'Motor',
  transmission: 'Transmissão',
  suspension: 'Suspensão',
  brakes: 'Freios',
  electrical: 'Elétrica',
  wheelsTires: 'Rodas/Pneus',
  bodyPaint: 'Carroceria/Pintura',
  interior: 'Interior',
  other: 'Outros',
}

export const reminderStateLabels: Record<Schemas['ReminderState'], string> = {
  ok: 'Em dia',
  dueSoon: 'Próximo',
  overdue: 'Vencido',
}

/** Unidade da peça como aparece ao lado da quantidade ("4 L"). */
export const partUnitLabels: Record<Schemas['PartUnit'], string> = {
  unit: 'un',
  liter: 'L',
  kilogram: 'kg',
  meter: 'm',
}

/** Unidade da peça por extenso, para a lista de escolha. */
export const partUnitNames: Record<Schemas['PartUnit'], string> = {
  unit: 'Unidade (un)',
  liter: 'Litro (L)',
  kilogram: 'Quilo (kg)',
  meter: 'Metro (m)',
}

// Os valores na ordem das tabelas acima (que o tsc obriga a ter todos), para montar listas de escolha e filtros.
export const recordTypes = Object.keys(recordTypeLabels) as Schemas['RecordType'][]
export const categories = Object.keys(categoryLabels) as Schemas['Category'][]
export const partUnits = Object.keys(partUnitLabels) as Schemas['PartUnit'][]

export const fuelLabels: Record<Schemas['Fuel'], string> = {
  gasoline: 'Gasolina',
  ethanol: 'Álcool',
  flex: 'Flex',
  diesel: 'Diesel',
  hybrid: 'Híbrido',
  electric: 'Elétrico',
  other: 'Outro',
}
