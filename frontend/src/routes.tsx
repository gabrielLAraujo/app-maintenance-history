import type { RouteObject } from 'react-router'
import { AuthRoot, GuestOnly, RequireAuth } from './auth/RouteGuards'
import { Layout } from './components/Layout'
import { Dashboard } from './pages/Dashboard'
import { Login } from './pages/Login'
import { NotFound } from './pages/NotFound'
import { PublicVehicle } from './pages/PublicVehicle'
import { RecordDetail } from './pages/RecordDetail'
import { RecordForm } from './pages/RecordForm'
import { Register } from './pages/Register'
import { VehicleDetail } from './pages/VehicleDetail'
import { VehicleEdit } from './pages/VehicleEdit'
import { VehicleNew } from './pages/VehicleNew'

/**
 * Mapa de rotas do app. As tasks seguintes acrescentam as telas logadas como filhas do Layout.
 *  - /p/:token: pública, sem login, sem o Layout e sem sessão (fica fora do AuthRoot: não consulta /api/auth/*);
 *  - /login e /register: só para visitantes (/register só abre se `registrationEnabled`);
 *  - o resto exige login e usa o Layout; sem sessão, o RequireAuth leva ao /login.
 */
export const routes: RouteObject[] = [
  { path: '/p/:token', element: <PublicVehicle /> },
  {
    element: <AuthRoot />,
    children: [
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
            element: <Layout />,
            children: [
              { index: true, element: <Dashboard /> },
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
