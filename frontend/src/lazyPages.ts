import { lazy } from 'react'

/*
 * Telas carregadas sob demanda. A landing, a página pública e o 404 vão no arquivo principal; o resto (moldura do
 * app, telas logadas, login e cadastro) só baixa quando é aberto. Assim o visitante que chega pela landing não paga
 * pelo código dos formulários e das telas do veículo (Lighthouse no celular: o JS inicial atrasava a primeira tela).
 */
export const AppShell = lazy(() => import('./components/AppShell').then((m) => ({ default: m.AppShell })))
export const Dashboard = lazy(() => import('./pages/Dashboard').then((m) => ({ default: m.Dashboard })))
export const Login = lazy(() => import('./pages/Login').then((m) => ({ default: m.Login })))
export const Register = lazy(() => import('./pages/Register').then((m) => ({ default: m.Register })))
export const Vehicles = lazy(() => import('./pages/Vehicles').then((m) => ({ default: m.Vehicles })))
export const Reminders = lazy(() => import('./pages/Reminders').then((m) => ({ default: m.Reminders })))
export const VehicleNew = lazy(() => import('./pages/VehicleNew').then((m) => ({ default: m.VehicleNew })))
export const VehicleDetail = lazy(() => import('./pages/VehicleDetail').then((m) => ({ default: m.VehicleDetail })))
export const VehicleEdit = lazy(() => import('./pages/VehicleEdit').then((m) => ({ default: m.VehicleEdit })))
export const RecordForm = lazy(() => import('./pages/RecordForm').then((m) => ({ default: m.RecordForm })))
export const RecordDetail = lazy(() => import('./pages/RecordDetail').then((m) => ({ default: m.RecordDetail })))
