import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { AcceptInvitationPage, BootstrapPage, LoginPage } from "./pages/AuthPages";
import { BillingPage } from "./pages/BillingPage";
import { BrandingPage } from "./pages/BrandingPage";
import { CustomersPage } from "./pages/CustomersPage";
import { CustomerDetailPage } from "./pages/CustomerDetailPage";
import { DashboardPage } from "./pages/DashboardPage";
import { IntegrationsPage } from "./pages/IntegrationsPage";
import { ReportsPage } from "./pages/ReportsPage";
import { SettingsPage } from "./pages/SettingsPage";
import { TeamPage } from "./pages/TeamPage";
import { VisitDetailPage } from "./pages/VisitDetailPage";
import { VisitsPage } from "./pages/VisitsPage";

export function App(){return <Routes><Route path="login" element={<LoginPage/>}/><Route path="bootstrap" element={<BootstrapPage/>}/><Route path="accept-invitation" element={<AcceptInvitationPage/>}/><Route element={<ProtectedRoute/>}><Route element={<AppShell/>}><Route index element={<DashboardPage/>}/><Route path="customers" element={<CustomersPage/>}/><Route path="customers/:id" element={<CustomerDetailPage/>}/><Route path="visits" element={<VisitsPage/>}/><Route path="visits/:id" element={<VisitDetailPage/>}/><Route path="team" element={<TeamPage/>}/><Route path="branding" element={<BrandingPage/>}/><Route path="billing" element={<BillingPage/>}/><Route path="reports" element={<ReportsPage/>}/><Route path="integrations" element={<IntegrationsPage/>}/><Route path="settings" element={<SettingsPage/>}/><Route path="*" element={<Navigate to="/" replace/>}/></Route></Route></Routes>}
