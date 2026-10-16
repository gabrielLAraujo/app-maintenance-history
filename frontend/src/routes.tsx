import type { RouteObject } from 'react-router'
import { RootRoute } from './auth/RootRoute'
import { AuthRoot, GuestOnly, RequireAuth } from './auth/RouteGuards'
import {
  AppShell,
  Dashboard,
  Login,
  RecordDetail,
  RecordForm,
  Register,
  Reminders,
  VehicleDetail,
  VehicleEdit,
  VehicleNew,
  Vehicles,
} from './lazyPages'
import { NotFound } from './pages/NotFound'
import { PublicVehicle } from './pages/PublicVehicle'

/**
 * Mapa de rotas do app. As telas logadas são filhas do AppShell (menu lateral e faixa da demo).
 *  - /p/:token: pública, sem login, sem o AppShell e sem sessão (fica fora do AuthRoot: não consulta /api/auth/*);
 *  - /: a landing para visitantes e o painel (no AppShell) para quem tem sessão;
 *  - /login e /register: só para visitantes (/register só abre se `registrationEnabled`);
 *  - o resto exige login e usa o AppShell; sem sessão, o RequireAuth leva ao /login.
 * Só a landing, a página pública e o 404 vêm no arquivo principal; as outras telas carregam sob demanda (lazyPages).
 */
export const routes: RouteObject[] = [
  { path: '/p/:token', element: <PublicVehicle /> },
  {
    element: <AuthRoot />,
    children: [
      { path: '/', element: <RootRoute />, children: [{ index: true, element: <Dashboard /> }] },
      {
        element: <GuestOnly />,
        children: [
          { path: '/login', element: <Login /> },
          { path: '/register', element: <Register /> },
        ],
      },
      {
        element: <RequireAuth />,
        children: [
          {
            element: <AppShell />,
            children: [
              { path: '/vehicles', element: <Vehicles /> },
              { path: '/reminders', element: <Reminders /> },
              { path: '/vehicles/new', element: <VehicleNew /> },
              { path: '/vehicles/:id', element: <VehicleDetail /> },
              { path: '/vehicles/:id/edit', element: <VehicleEdit /> },
              { path: '/vehicles/:id/records/new', element: <RecordForm /> },
              { path: '/vehicles/:id/records/:recordId', element: <RecordDetail /> },
              { path: '/vehicles/:id/records/:recordId/edit', element: <RecordForm /> },
            ],
          },
        ],
      },
      { path: '*', element: <NotFound /> },
    ],
  },
]
