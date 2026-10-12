import type { components } from '../api/schema'

type Schemas = components['schemas']

// Record<enum, string>: se a API ganhar um valor novo, o tsc aponta aqui o rótulo que falta.

export const vehicleTypeLabels: Record<Schemas['VehicleType'], string> = {
  car: 'Carro',
  motorcycle: 'Moto',
  truck: 'Caminhão',
}

export const fuelLabels: Record<Schemas['Fuel'], string> = {
  gasoline: 'Gasolina',
  ethanol: 'Álcool',
  flex: 'Flex',
  diesel: 'Diesel',
  hybrid: 'Híbrido',
  electric: 'Elétrico',
  other: 'Outro',
}
